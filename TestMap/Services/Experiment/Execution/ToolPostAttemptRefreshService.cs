using TestMap.App;
using TestMap.Models.AgentTools;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.Experiment.Execution;

public sealed class ToolPostAttemptRefreshResult
{
    public bool Refreshed { get; init; }
    public int? SolutionId { get; init; }
    public string SkipReason { get; init; } = string.Empty;
}

public interface IToolPostAttemptRefreshService
{
    Task<ToolPostAttemptRefreshResult> RefreshAsync(
        CandidateMethodContext methodContext,
        ToolAttempt attempt,
        CancellationToken cancellationToken = default);
}

public sealed class ToolPostAttemptRefreshService : IToolPostAttemptRefreshService
{
    private readonly ProjectContext _context;
    private readonly ISourceTestMappingRefreshService _sourceTestMappingRefreshService;

    public ToolPostAttemptRefreshService(
        ProjectContext context,
        ISourceTestMappingRefreshService sourceTestMappingRefreshService)
    {
        _context = context;
        _sourceTestMappingRefreshService = sourceTestMappingRefreshService;
    }

    public async Task<ToolPostAttemptRefreshResult> RefreshAsync(
        CandidateMethodContext methodContext,
        ToolAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        var solutionId = ResolveSolutionId(methodContext);
        if (solutionId <= 0)
        {
            return new ToolPostAttemptRefreshResult
            {
                Refreshed = false,
                SkipReason = $"No solution id could be resolved for source project '{methodContext.SourceProjectPath}'."
            };
        }

        await _sourceTestMappingRefreshService.RefreshForProjectAsync(
            _context.Project.DbId,
            solutionId,
            cancellationToken);

        return new ToolPostAttemptRefreshResult
        {
            Refreshed = true,
            SolutionId = solutionId
        };
    }

    private int ResolveSolutionId(CandidateMethodContext methodContext)
    {
        var sourceProjectPath = NormalizeProjectPath(methodContext.SourceProjectPath);
        var project = _context.Project.Projects.FirstOrDefault(x =>
            string.Equals(NormalizeProjectPath(x.FilePath), sourceProjectPath, StringComparison.OrdinalIgnoreCase));
        if (project?.SolutionId > 0)
            return project.SolutionId;

        var solution = _context.Project.Solutions.FirstOrDefault(x =>
            x.Projects.Any(projectPath =>
                string.Equals(NormalizeProjectPath(projectPath), sourceProjectPath, StringComparison.OrdinalIgnoreCase)));
        return solution?.Id ?? 0;
    }

    private static string NormalizeProjectPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
