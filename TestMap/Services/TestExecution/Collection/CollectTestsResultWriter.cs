using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Results;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestExecution.Collection;

public class CollectTestsResultWriter
{
    private readonly ProjectContext _context;
    private readonly TestMapDbContext _dbContext;
    private readonly TestMapConfig _config;
    private readonly BuildTestService _buildTestService;
    private readonly IMethodSelectionService _methodSelectionService;
    private readonly CandidateInventoryRepository _candidateInventoryRepository;

    public CollectTestsResultWriter(
        ProjectContext context,
        TestMapDbContext dbContext,
        TestMapConfig config,
        BuildTestService buildTestService,
        IMethodSelectionService methodSelectionService,
        CandidateInventoryRepository candidateInventoryRepository)
    {
        _context = context;
        _dbContext = dbContext;
        _config = config;
        _buildTestService = buildTestService;
        _methodSelectionService = methodSelectionService;
        _candidateInventoryRepository = candidateInventoryRepository;
    }

    public async Task WriteAsync(CancellationToken cancellationToken = default)
    {
        var projectId = _context.Project.DbId;
        var selectionConfig = CreateSelectionConfiguration();
        await _methodSelectionService.SelectCandidateMethodsAsync(selectionConfig, cancellationToken: cancellationToken);
        var strategy = selectionConfig.CandidateSelectionStrategy ??
                       _config.TestingConfig.GenerationConfig.TargetSelection.Strategy;
        var candidateCount = await _candidateInventoryRepository.CountAsync(projectId, strategy, cancellationToken);
        var eligibleCandidateCount = await _candidateInventoryRepository.CountEligibleAsync(
            projectId,
            strategy,
            cancellationToken);
        var executionSupportSummary = CreateExecutionSupportSummary();
        var latestRun = await _dbContext.TestRuns
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var result = new ProjectValidationResult(
            _context.Project.GitHubUrl,
            _context.Project.Owner,
            _context.Project.RepoName,
            _buildTestService.LatestRestoreSucceeded,
            _buildTestService.LatestBuildSucceeded,
            _buildTestService.LatestTestsExecuted && latestRun != null,
            latestRun?.Success == true,
            await _dbContext.CoverageReports.AnyAsync(x => x.ProjectId == projectId, cancellationToken),
            latestRun?.MutationScore.HasValue == true ||
            await _dbContext.MutationTestingReports.AnyAsync(x => x.ProjectId == projectId, cancellationToken),
            candidateCount > 0,
            candidateCount,
            eligibleCandidateCount,
            _buildTestService.LatestDockerContext,
            _buildTestService.LatestDockerOs,
            executionSupportSummary.Support,
            executionSupportSummary.UnsupportedProjectCount,
            executionSupportSummary.UnsupportedProjects,
            latestRun?.RunId ?? string.Empty,
            latestRun?.FailureAnalysis?.Category ?? string.Empty,
            latestRun?.FailureAnalysis?.Summary ?? string.Empty);

        await WriteCsvRowAsync(result, cancellationToken);
    }

    private ExperimentConfig CreateSelectionConfiguration()
    {
        var generationConfig = _config.TestingConfig.GenerationConfig;
        var experimentConfig = _config.ExperimentConfig;

        return new ExperimentConfig
        {
            CandidateLimit = generationConfig.TargetSelection.CandidateLimit,
            MinCoverageThreshold = experimentConfig.MinCoverageThreshold,
            MaxCoverageThreshold = experimentConfig.MaxCoverageThreshold,
            CandidateSelectionStrategy = generationConfig.TargetSelection.Strategy,
            GenerationApproach = generationConfig.Strategy
        };
    }

    private ExecutionSupportSummary CreateExecutionSupportSummary()
    {
        var projectSummaries = _context.Project.Projects
            .Select(project => new ProjectExecutionSupportSummary(
                Path.GetFileName(project.FilePath),
                project.BuildMetadata.ExecutionSupport,
                project.BuildMetadata.BuildTargets.Count > 0
                    ? project.BuildMetadata.BuildTargets
                    : project.BuildTargets))
            .ToList();

        var support = ResolveAggregateExecutionSupport(projectSummaries.Select(x => x.Support));
        var unsupportedProjects = projectSummaries
            .Where(x => x.Support != ExecutionSupportType.Supported)
            .Select(x =>
            {
                var targets = x.BuildTargets.Count == 0 ? "no-targets" : string.Join("|", x.BuildTargets);
                return $"{x.ProjectName}:{x.Support}[{targets}]";
            })
            .ToList();

        return new ExecutionSupportSummary(
            support.ToString(),
            unsupportedProjects.Count,
            string.Join("; ", unsupportedProjects));
    }

    private static ExecutionSupportType ResolveAggregateExecutionSupport(IEnumerable<ExecutionSupportType> supportValues)
    {
        var values = supportValues.ToList();
        if (values.Count == 0) return ExecutionSupportType.Unknown;
        if (values.Any(x => x == ExecutionSupportType.UnsupportedWorkload))
            return ExecutionSupportType.UnsupportedWorkload;
        if (values.Any(x => x == ExecutionSupportType.UnsupportedPlatform))
            return ExecutionSupportType.UnsupportedPlatform;
        if (values.Any(x => x == ExecutionSupportType.Unknown))
            return ExecutionSupportType.Unknown;
        return ExecutionSupportType.Supported;
    }

    private async Task WriteCsvRowAsync(ProjectValidationResult result, CancellationToken cancellationToken)
    {
        var outputRoot = Directory.GetParent(_context.Project.OutputPath ?? string.Empty)?.FullName
                         ?? _context.Project.OutputPath
                         ?? _context.Project.DirectoryPath;
        var csvPath = Path.Combine(outputRoot, "project-validation.csv");

        var fileExists = File.Exists(csvPath);
        await using var writer = new StreamWriter(csvPath, true);

        if (!fileExists)
            await writer.WriteLineAsync(
                "URL,Owner,Repo,Restores,Builds,TestsRun,TestsPass,HasCoverage,HasMutationScore,HasCandidateMethods,CandidateCount,ExperimentEligibleCandidateCount,DockerContext,DockerOs,ExecutionSupport,UnsupportedProjectCount,UnsupportedProjects,BaselineRunId,FailureCategory,FailureSummary".AsMemory(),
                cancellationToken);

        await writer.WriteLineAsync(
            $"{Escape(result.Url)},{Escape(result.Owner)},{Escape(result.Repo)},{result.Restores},{result.Builds},{result.TestsRun},{result.TestsPass},{result.HasCoverage},{result.HasMutationScore},{result.HasCandidateMethods},{result.CandidateCount},{result.ExperimentEligibleCandidateCount},{Escape(result.DockerContext)},{Escape(result.DockerOs)},{Escape(result.ExecutionSupport)},{result.UnsupportedProjectCount},{Escape(result.UnsupportedProjects)},{Escape(result.BaselineRunId)},{Escape(result.FailureCategory)},{Escape(result.FailureSummary)}"
                .AsMemory(),
            cancellationToken);
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private sealed record ExecutionSupportSummary(
        string Support,
        int UnsupportedProjectCount,
        string UnsupportedProjects);

    private sealed record ProjectExecutionSupportSummary(
        string ProjectName,
        ExecutionSupportType Support,
        List<string> BuildTargets);
}
