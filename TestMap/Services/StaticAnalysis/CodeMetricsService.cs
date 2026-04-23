using DotNetCodeMetrics.Core;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Mapping.Code;
using TestMap.Persistence.Ef.Repositories.Code;

namespace TestMap.Services.StaticAnalysis;

using CodeLocation = TestMap.Models.Code.Location;

public class CodeMetricsService : ICodeMetricsService
{
    private const string ObjectEntityType = "object";
    private const string MemberEntityType = "member";

    private readonly ProjectContext _context;
    private readonly FileRepository _fileRepository;
    private readonly ObjectRepository _objectRepository;
    private readonly MemberRepository _memberRepository;
    private readonly CodeMetricRepository _codeMetricRepository;
    private readonly TestMapDbContext _dbContext;
    private readonly IStaticAnalysisWorkspace _staticAnalysisWorkspace;

    public CodeMetricsService(
        ProjectContext context,
        FileRepository fileRepository,
        ObjectRepository objectRepository,
        MemberRepository memberRepository,
        CodeMetricRepository codeMetricRepository,
        TestMapDbContext dbContext,
        IStaticAnalysisWorkspace staticAnalysisWorkspace)
    {
        _context = context;
        _fileRepository = fileRepository;
        _objectRepository = objectRepository;
        _memberRepository = memberRepository;
        _codeMetricRepository = codeMetricRepository;
        _dbContext = dbContext;
        _staticAnalysisWorkspace = staticAnalysisWorkspace;
    }

    public async Task CollectCodeMetricsAsync(
        CSharpSolutionModel analysisSolution,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(analysisSolution.FilePath) || !File.Exists(analysisSolution.FilePath))
        {
            _context.Logger.Warning("Skipping code metrics. Solution file was not found: {SolutionFilePath}", analysisSolution.FilePath);
            return;
        }

        var solutionProjectPaths = new HashSet<string>(analysisSolution.Projects, StringComparer.OrdinalIgnoreCase);
        var solution = await _staticAnalysisWorkspace.OpenSolutionAsync(analysisSolution.FilePath, cancellationToken);
        var metricResults = await DotNetCodeMetricsAnalyzer.GetMetricsAsync(
            solution,
            CreateMetricOptions(),
            cancellationToken);
        var filteredMetricResults = metricResults
            .Where(metric => metric.ProjectFilePath != null && solutionProjectPaths.Contains(metric.ProjectFilePath))
            .ToList();

        await PersistMetricResultsAsync(
            filteredMetricResults,
            analysisSolution.Projects,
            analysisSolution.FilePath,
            cancellationToken);
    }

    public async Task CollectCodeMetricsAsync(
        CSharpProjectModel analysisProject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(analysisProject.FilePath) || !File.Exists(analysisProject.FilePath))
        {
            _context.Logger.Warning("Skipping code metrics. Project file was not found: {ProjectFilePath}", analysisProject.FilePath);
            return;
        }

        var project = await _staticAnalysisWorkspace.OpenProjectAsync(analysisProject.FilePath, cancellationToken);
        var metricResults = await DotNetCodeMetricsAnalyzer.GetMetricsAsync(
            project,
            CreateMetricOptions(),
            cancellationToken);

        await PersistMetricResultsAsync(
            metricResults,
            [analysisProject.FilePath],
            analysisProject.FilePath,
            cancellationToken);
    }

    private async Task PersistMetricResultsAsync(
        IEnumerable<CodeMetricResult> metricResults,
        IReadOnlyCollection<string> analysisProjectPaths,
        string targetPath,
        CancellationToken cancellationToken)
    {
        var projectIds = _context.Project.Projects
            .Where(project => analysisProjectPaths.Contains(project.FilePath, StringComparer.OrdinalIgnoreCase))
            .Where(project => project.Id != 0)
            .Select(project => project.Id)
            .ToList();

        if (projectIds.Count == 0)
        {
            _context.Logger.Warning("Skipping code metrics persistence. No persisted project IDs were found for {TargetPath}", targetPath);
            return;
        }

        var files = await _dbContext.Files
            .Where(file => projectIds.Contains(file.CSharpProjectId))
            .ToListAsync(cancellationToken);
        var fileIds = files.Select(file => file.Id).ToList();

        var objects = await _dbContext.Objects
            .Where(item => fileIds.Contains(item.FileId))
            .ToListAsync(cancellationToken);
        var objectIds = objects.Select(item => item.Id).ToList();

        var members = await _dbContext.Members
            .Where(item => objectIds.Contains(item.ObjectEntityId))
            .ToListAsync(cancellationToken);

        var fileModels = files.Select(file => file.ToDomain()).ToList();
        var objectModels = objects.Select(item => item.ToDomain()).ToList();
        var memberModels = members.Select(item => item.ToDomain()).ToList();

        var fileIdsByPath = fileModels
            .Where(file => !string.IsNullOrWhiteSpace(file.FilePath))
            .GroupBy(file => NormalizePath(file.FilePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        var objectsByFile = objectModels
            .GroupBy(item => item.FileId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var membersByObjectId = memberModels
            .GroupBy(item => item.ObjectEntityId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var persistedCount = 0;
        var skippedCount = 0;
        var skippedSamples = new List<string>();

        foreach (var metricResult in metricResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsAccessorMetric(metricResult))
            {
                skippedCount++;
                continue;
            }

            if (metricResult.Location == null ||
                !fileIdsByPath.TryGetValue(NormalizePath(metricResult.Location.FilePath), out var fileId))
            {
                skippedCount++;
                AddSkippedSample(skippedSamples, metricResult, "file");
                continue;
            }

            if (metricResult.Kind == CodeMetricTargetKind.Type)
            {
                if (TryFindObject(metricResult, fileId, objectsByFile, out var objectModel))
                {
                    await _codeMetricRepository.InsertOrUpdateAsync(CreateMetricModel(metricResult, objectModel.Id, ObjectEntityType));
                    persistedCount++;
                    continue;
                }
            }
            else if (TryFindMember(metricResult, fileId, objectsByFile, membersByObjectId, out var memberModel))
            {
                await _codeMetricRepository.InsertOrUpdateAsync(CreateMetricModel(metricResult, memberModel.Id, MemberEntityType));
                persistedCount++;
                continue;
            }

            skippedCount++;
            AddSkippedSample(skippedSamples, metricResult, "entity");
        }

        _context.Logger.Information(
            "Collected {PersistedCount} code metric rows for {ProjectFilePath}. Skipped {SkippedCount} unmatched metrics.",
            persistedCount,
            targetPath,
            skippedCount);

        foreach (var skippedSample in skippedSamples)
        {
            _context.Logger.Warning("Unmatched code metric sample: {SkippedMetric}", skippedSample);
        }
    }

    private static CodeMetricsOptions CreateMetricOptions()
        => new()
        {
            Quiet = true,
            TargetKinds =
            [
                CodeMetricTargetKind.Type,
                CodeMetricTargetKind.Method,
                CodeMetricTargetKind.Property,
                CodeMetricTargetKind.Field,
                CodeMetricTargetKind.Event
            ]
        };

    private static bool TryFindObject(
        CodeMetricResult metricResult,
        int fileId,
        Dictionary<int, List<ObjectModel>> objectsByFile,
        out ObjectModel objectModel)
    {
        objectModel = null!;

        if (!objectsByFile.TryGetValue(fileId, out var candidates) || metricResult.Location == null)
        {
            return false;
        }

        var metricLine = ToZeroBasedLine(metricResult.Location.StartLine);
        var metricEndLine = ToZeroBasedLine(metricResult.Location.EndLine);
        var metricName = GetSimpleName(metricResult);

        var match = candidates
            .Where(candidate => Overlaps(candidate.Location, metricLine, metricEndLine))
            .OrderByDescending(candidate => NamesMatch(candidate.Name, metricName, metricResult.FullyQualifiedName))
            .ThenBy(candidate => GetLineSpan(candidate.Location))
            .FirstOrDefault(candidate => NamesMatch(candidate.Name, metricName, metricResult.FullyQualifiedName))
            ?? candidates
                .Where(candidate => Overlaps(candidate.Location, metricLine, metricEndLine))
                .OrderBy(candidate => GetLineSpan(candidate.Location))
                .FirstOrDefault();

        if (match == null)
        {
            return false;
        }

        objectModel = match;
        return true;
    }

    private static bool TryFindMember(
        CodeMetricResult metricResult,
        int fileId,
        Dictionary<int, List<ObjectModel>> objectsByFile,
        Dictionary<int, List<MemberModel>> membersByObjectId,
        out MemberModel memberModel)
    {
        memberModel = null!;

        if (!objectsByFile.TryGetValue(fileId, out var objectsInFile) || metricResult.Location == null)
        {
            return false;
        }

        var metricLine = ToZeroBasedLine(metricResult.Location.StartLine);
        var metricEndLine = ToZeroBasedLine(metricResult.Location.EndLine);
        var metricName = GetSimpleName(metricResult);
        var containingObjects = objectsInFile
            .Where(candidate => Overlaps(candidate.Location, metricLine, metricEndLine))
            .OrderBy(candidate => GetLineSpan(candidate.Location))
            .ToList();

        foreach (var containingObject in containingObjects)
        {
            if (!membersByObjectId.TryGetValue(containingObject.Id, out var members))
            {
                continue;
            }

            var match = members
                .Where(candidate => Overlaps(candidate.Location, metricLine, metricEndLine))
                .OrderByDescending(candidate => NamesMatch(candidate.Name, metricName, metricResult.FullyQualifiedName))
                .ThenBy(candidate => GetLineSpan(candidate.Location))
                .FirstOrDefault(candidate => NamesMatch(candidate.Name, metricName, metricResult.FullyQualifiedName))
                ?? members
                    .Where(candidate => Overlaps(candidate.Location, metricLine, metricEndLine))
                    .OrderBy(candidate => GetLineSpan(candidate.Location))
                    .FirstOrDefault();

            if (match != null)
            {
                memberModel = match;
                return true;
            }
        }

        return false;
    }

    private static CodeMetricsModel CreateMetricModel(CodeMetricResult metricResult, int entityId, string entityType)
        => new(
            entityType: entityType,
            entityId: entityId,
            maintainabilityIndex: metricResult.MaintainabilityIndex,
            cyclomaticComplexity: metricResult.CyclomaticComplexity,
            classCoupling: metricResult.ClassCoupling,
            depthOfInheritance: metricResult.DepthOfInheritance ?? 0,
            sourceLinesOfCode: ToInt32(metricResult.SourceLines),
            executableLinesOfCode: ToInt32(metricResult.ExecutableLines));

    private static bool IsAccessorMetric(CodeMetricResult metricResult)
        => metricResult.Kind == CodeMetricTargetKind.Method &&
           (metricResult.FullyQualifiedName.EndsWith(".get", StringComparison.Ordinal) ||
            metricResult.FullyQualifiedName.EndsWith(".set", StringComparison.Ordinal) ||
            metricResult.FullyQualifiedName.EndsWith(".add", StringComparison.Ordinal) ||
            metricResult.FullyQualifiedName.EndsWith(".remove", StringComparison.Ordinal));

    private static bool ContainsLine(CodeLocation location, int zeroBasedLine)
        => location.StartLineNumber <= zeroBasedLine && zeroBasedLine <= location.EndLineNumber;

    private static bool Overlaps(CodeLocation location, int zeroBasedStartLine, int zeroBasedEndLine)
        => ContainsLine(location, zeroBasedStartLine)
           || ContainsLine(location, zeroBasedEndLine)
           || zeroBasedStartLine <= location.StartLineNumber && location.EndLineNumber <= zeroBasedEndLine;

    private static int GetLineSpan(CodeLocation location)
        => Math.Max(0, location.EndLineNumber - location.StartLineNumber);

    private static int ToZeroBasedLine(int oneBasedLine)
        => Math.Max(0, oneBasedLine - 1);

    private static int ToInt32(long value)
    {
        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value < int.MinValue)
        {
            return int.MinValue;
        }

        return (int)value;
    }

    private static bool NamesMatch(string modelName, string metricName, string fullyQualifiedName)
    {
        if (string.Equals(modelName, metricName, StringComparison.Ordinal))
        {
            return true;
        }

        if ((metricName is ".ctor" or "#ctor" or "ctor" or ".cctor" or "#cctor" or "cctor") &&
            fullyQualifiedName.EndsWith("." + modelName, StringComparison.Ordinal))
        {
            return true;
        }

        return fullyQualifiedName.EndsWith("." + modelName, StringComparison.Ordinal)
               || fullyQualifiedName.Contains("." + modelName + "(", StringComparison.Ordinal)
               || fullyQualifiedName.Contains("." + modelName + "<", StringComparison.Ordinal)
               || fullyQualifiedName.Contains("#" + modelName + "(", StringComparison.Ordinal)
               || fullyQualifiedName.Contains("#" + modelName + "<", StringComparison.Ordinal);
    }

    private static string GetSimpleName(CodeMetricResult metricResult)
    {
        var name = metricResult.Name;
        var parenIndex = name.IndexOf('(');
        if (parenIndex >= 0)
        {
            name = name[..parenIndex];
        }

        var lastDotIndex = name.LastIndexOf('.');
        if (lastDotIndex >= 0 && lastDotIndex < name.Length - 1)
        {
            name = name[(lastDotIndex + 1)..];
        }

        var genericIndex = name.IndexOf('<');
        if (genericIndex >= 0)
        {
            name = name[..genericIndex];
        }

        return name;
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void AddSkippedSample(List<string> skippedSamples, CodeMetricResult metricResult, string reason)
    {
        if (skippedSamples.Count >= 10)
        {
            return;
        }

        skippedSamples.Add(
            $"{reason}: {metricResult.Kind} {metricResult.FullyQualifiedName} @ {metricResult.Location?.FilePath}:{metricResult.Location?.StartLine}");
    }
}
