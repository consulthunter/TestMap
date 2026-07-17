using Serilog;
using TestMap.Models;
using TestMap.Models.Generation;
using TestMap.Models.Targets;

namespace TestMap.App;

public class ProjectContext
{
    public ProjectModel Project { get; init; }
    public ILogger Logger => Project.Logger!;

    // Optional runtime-only state
    public string? RepoPath { get; set; }
    public string? CurrentCommit { get; set; }
    public RepositoryTarget? RepositoryTarget => Project.RepositoryTarget;
    public MaterializedRevision? MaterializedRevision
    {
        get => Project.MaterializedRevision;
        set => Project.MaterializedRevision = value;
    }
    public string? VerifiedBaseCommit => MaterializedRevision is { Status: MaterializationStatus.Available } revision
        ? revision.ResolvedCommit
        : null;
    public TestBootstrapRuntimeState? TestBootstrapState { get; set; }

    public ProjectContext(
        ProjectModel project
    )
    {
        Project = project;
    }
}
