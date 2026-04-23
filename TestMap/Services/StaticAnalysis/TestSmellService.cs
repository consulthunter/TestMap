using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Results;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Testing;
using XNoseNext.Core;
using XNoseNext.Core.Findings;

namespace TestMap.Services.StaticAnalysis;

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
        {
            throw new ArgumentException("Solution or project path is required.", nameof(solutionOrProjectPath));
        }

        var result = Path.GetExtension(solutionOrProjectPath) switch
        {
            var extension when extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                => await AnalyzeCachedSolutionAsync(solutionOrProjectPath, cancellationToken),
            var extension when extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                => await AnalyzeCachedProjectAsync(solutionOrProjectPath, cancellationToken),
            _ => await _detector.AnalyzeAsync(solutionOrProjectPath, cancellationToken)
        };

        foreach (var finding in result.Findings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var memberId = await ResolveMemberIdAsync(finding, cancellationToken);
            var objectId = memberId == null
                ? await ResolveObjectIdAsync(finding, cancellationToken)
                : null;

            if (memberId == null && finding.TestMethod != null)
            {
                _context.Logger?.Warning(
                    "Could not resolve test smell {SmellId} to member {Type}.{Method} in {FilePath}:{Line}",
                    finding.SmellId,
                    finding.TestMethod.ContainingTypeName,
                    finding.TestMethod.MethodName,
                    finding.FileLocation.FilePath,
                    finding.FileLocation.Line);
            }

            await _testSmellRepository.InsertOrUpdateAsync(
                CreateModel(finding, result.AnalyzedAtUtc, projectId, memberId, objectId),
                cancellationToken);
        }
    }

    private async Task<int?> ResolveMemberIdAsync(
        TestSmellFinding finding,
        CancellationToken cancellationToken)
    {
        if (finding.TestMethod == null)
        {
            return null;
        }

        var candidates = await _dbContext.Members
            .Join(
                _dbContext.Objects,
                member => member.ObjectEntityId,
                obj => obj.Id,
                (member, obj) => new { Member = member, Object = obj })
            .Join(
                _dbContext.Files,
                memberObject => memberObject.Object.FileId,
                file => file.Id,
                (memberObject, file) => new
                {
                    memberObject.Member,
                    memberObject.Object,
                    File = file
                })
            .Where(x => x.Member.Name == finding.TestMethod.MethodName)
            .Where(x => x.Object.Name == finding.TestMethod.ContainingTypeName)
            .Where(x => x.Member.IsTestMember)
            .ToListAsync(cancellationToken);

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

    private async Task<int?> ResolveObjectIdAsync(
        TestSmellFinding finding,
        CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.Objects
            .Join(
                _dbContext.Files,
                obj => obj.FileId,
                file => file.Id,
                (obj, file) => new { Object = obj, File = file })
            .Where(x => x.Object.Name == finding.ContainingTypeName())
            .ToListAsync(cancellationToken);

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
}

internal static class TestSmellFindingExtensions
{
    public static string ContainingTypeName(this TestSmellFinding finding)
    {
        return finding.TestMethod?.ContainingTypeName ?? string.Empty;
    }
}
