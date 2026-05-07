using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Results;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Persistence.Ef.Mappings;
using TestMap.Persistence.Ef.Repositories.Testing;
using XNoseNext.Core;
using XNoseNext.Core.Findings;

namespace TestMap.Services.StaticAnalysis.Enrichment;

public interface ITestSmellService
{
    Task CollectAsync(
        string solutionOrProjectPath,
        int projectId,
        CancellationToken cancellationToken = default);
}

public class TestSmellService : ITestSmellService
{
    private readonly ProjectContext _context;
    private readonly TestMapDbContext _dbContext;
    private readonly TestSmellRepository _testSmellRepository;
    private readonly TestSmellDetector _detector;
    private readonly IStaticAnalysisWorkspace _staticAnalysisWorkspace;

    public TestSmellService(
        ProjectContext context,
        TestMapDbContext dbContext,
        TestSmellRepository testSmellRepository,
        IStaticAnalysisWorkspace staticAnalysisWorkspace)
        : this(context, dbContext, testSmellRepository, staticAnalysisWorkspace, new TestSmellDetector())
    {
    }

    public TestSmellService(
        ProjectContext context,
        TestMapDbContext dbContext,
        TestSmellRepository testSmellRepository,
        IStaticAnalysisWorkspace staticAnalysisWorkspace,
        TestSmellDetector detector)
    {
        _context = context;
        _dbContext = dbContext;
        _testSmellRepository = testSmellRepository;
        _staticAnalysisWorkspace = staticAnalysisWorkspace;
        _detector = detector;
    }

    public async Task CollectAsync(
        string solutionOrProjectPath,
        int projectId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(solutionOrProjectPath))
            throw new ArgumentException("Solution or project path is required.", nameof(solutionOrProjectPath));

        var result = Path.GetExtension(solutionOrProjectPath) switch
        {
            var extension when extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                => await AnalyzeCachedSolutionAsync(solutionOrProjectPath, cancellationToken),
            var extension when extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                => await AnalyzeCachedProjectAsync(solutionOrProjectPath, cancellationToken),
            _ => await _detector.AnalyzeAsync(solutionOrProjectPath, cancellationToken)
        };

        var memberCandidates = await _dbContext.Members
            .Join(
                _dbContext.Objects,
                member => member.ObjectEntityId,
                obj => obj.Id,
                (member, obj) => new { Member = member, Object = obj })
            .Where(x => x.Member.IsTestMember)
            .Join(
                _dbContext.Files,
                memberObject => memberObject.Object.FileId,
                file => file.Id,
                (memberObject, file) => new MemberCandidate(memberObject.Member, memberObject.Object, file))
            .ToListAsync(cancellationToken);
        var objectCandidates = await _dbContext.Objects
            .Join(
                _dbContext.Files,
                obj => obj.FileId,
                file => file.Id,
                (obj, file) => new ObjectCandidate(obj, file))
            .ToListAsync(cancellationToken);
        var memberCandidatesByKey = memberCandidates
            .GroupBy(
                x => CreateMemberLookupKey(x.Object.Name, x.Member.Name),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var objectCandidatesByType = objectCandidates
            .GroupBy(x => x.Object.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var existingSmells = await _dbContext.TestSmells
            .Where(x => x.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var smellsByKey = existingSmells.ToDictionary(CreateSmellKey);

        foreach (var finding in result.Findings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var memberId = ResolveMemberId(finding, memberCandidatesByKey);
            var objectId = memberId == null
                ? ResolveObjectId(finding, objectCandidatesByType)
                : null;

            if (memberId == null && finding.TestMethod != null)
                _context.Logger?.Warning(
                    "Could not resolve test smell {SmellId} to member {Type}.{Method} in {FilePath}:{Line}",
                    finding.SmellId,
                    finding.TestMethod.ContainingTypeName,
                    finding.TestMethod.MethodName,
                    finding.FileLocation.FilePath,
                    finding.FileLocation.Line);

            UpsertSmell(
                CreateModel(finding, result.AnalyzedAtUtc, projectId, memberId, objectId),
                smellsByKey);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static int? ResolveMemberId(
        TestSmellFinding finding,
        IReadOnlyDictionary<string, List<MemberCandidate>> memberCandidatesByKey)
    {
        if (finding.TestMethod == null) return null;

        if (!memberCandidatesByKey.TryGetValue(
                CreateMemberLookupKey(finding.TestMethod.ContainingTypeName, finding.TestMethod.MethodName),
                out var candidates))
            return null;

        var normalizedFindingPath = NormalizePath(finding.FileLocation.FilePath);
        var lineZeroBased = finding.TestMethod.Location.Line is int line ? line - 1 : (int?)null;

        return candidates
            .Where(x => NormalizePath(x.File.FilePath) == normalizedFindingPath)
            .OrderBy(x => lineZeroBased == null
                ? 0
                : Math.Abs(x.Member.Location.StartLineNumber - lineZeroBased.Value))
            .Select(x => (int?)x.Member.Id)
            .FirstOrDefault();
    }

    private static int? ResolveObjectId(
        TestSmellFinding finding,
        IReadOnlyDictionary<string, List<ObjectCandidate>> objectCandidatesByType)
    {
        if (!objectCandidatesByType.TryGetValue(finding.ContainingTypeName(), out var candidates)) return null;

        var normalizedFindingPath = NormalizePath(finding.FileLocation.FilePath);
        var lineZeroBased = finding.FileLocation.Line is int line ? line - 1 : (int?)null;

        return candidates
            .Where(x => NormalizePath(x.File.FilePath) == normalizedFindingPath)
            .OrderBy(x => lineZeroBased == null
                ? 0
                : Math.Abs(x.Object.Location.StartLineNumber - lineZeroBased.Value))
            .Select(x => (int?)x.Object.Id)
            .FirstOrDefault();
    }

    private static TestSmellModel CreateModel(
        TestSmellFinding finding,
        DateTimeOffset analyzedAtUtc,
        int projectId,
        int? memberId,
        int? objectId)
    {
        return new TestSmellModel
        {
            ProjectId = projectId,
            MemberId = memberId,
            ObjectId = objectId,
            SmellId = finding.SmellId,
            SmellName = finding.SmellName,
            Message = finding.Message,
            FilePath = finding.FileLocation.FilePath,
            Line = finding.FileLocation.Line,
            Column = finding.FileLocation.Column,
            ContainingTypeName = finding.TestMethod?.ContainingTypeName ?? string.Empty,
            TestMethodName = finding.TestMethod?.MethodName ?? string.Empty,
            AnalyzedAtUtc = analyzedAtUtc,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
    }

    private async Task<TestSmellAnalysisResult> AnalyzeCachedSolutionAsync(
        string solutionPath,
        CancellationToken cancellationToken)
    {
        var solution = await _staticAnalysisWorkspace.OpenSolutionAsync(solutionPath, cancellationToken);
        return await _detector.AnalyzeAsync(solution, solutionPath, cancellationToken);
    }

    private async Task<TestSmellAnalysisResult> AnalyzeCachedProjectAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var project = await _staticAnalysisWorkspace.OpenProjectAsync(projectPath, cancellationToken);
        return await _detector.AnalyzeAsync(project, projectPath, cancellationToken);
    }

    private void UpsertSmell(
        TestSmellModel model,
        IDictionary<string, TestSmellEntity> smellsByKey)
    {
        var key = CreateSmellKey(model);
        if (smellsByKey.TryGetValue(key, out var existing))
        {
            if (TestSmellRepository.HasChanged(existing, model)) TestSmellRepository.Apply(existing, model);

            return;
        }

        var entity = model.ToEntity();
        _dbContext.TestSmells.Add(entity);
        smellsByKey[key] = entity;
    }

    private static string CreateMemberLookupKey(string containingTypeName, string methodName)
    {
        return containingTypeName + "::" + methodName;
    }

    private static string CreateSmellKey(TestSmellEntity entity)
    {
        return CreateSmellKey(
            entity.ProjectId,
            entity.MemberId,
            entity.ObjectId,
            entity.SmellId,
            entity.FilePath,
            entity.Line,
            entity.Column);
    }

    private static string CreateSmellKey(TestSmellModel model)
    {
        return CreateSmellKey(
            model.ProjectId,
            model.MemberId,
            model.ObjectId,
            model.SmellId,
            model.FilePath,
            model.Line,
            model.Column);
    }

    private static string CreateSmellKey(
        int projectId,
        int? memberId,
        int? objectId,
        string smellId,
        string filePath,
        int? line,
        int? column)
    {
        return string.Join(
            "|",
            projectId,
            memberId?.ToString() ?? string.Empty,
            objectId?.ToString() ?? string.Empty,
            smellId,
            filePath,
            line?.ToString() ?? string.Empty,
            column?.ToString() ?? string.Empty);
    }

    private sealed record MemberCandidate(
        Persistence.Ef.Entities.Code.MemberEntity Member,
        Persistence.Ef.Entities.Code.ObjectEntity Object,
        Persistence.Ef.Entities.Code.FileEntity File);

    private sealed record ObjectCandidate(
        Persistence.Ef.Entities.Code.ObjectEntity Object,
        Persistence.Ef.Entities.Code.FileEntity File);
}

internal static class TestSmellFindingExtensions
{
    public static string ContainingTypeName(this TestSmellFinding finding)
    {
        return finding.TestMethod?.ContainingTypeName ?? string.Empty;
    }
}
