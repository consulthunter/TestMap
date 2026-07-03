using Serilog;
using TestMap.Models;
using TestMap.Models.Generation;

namespace TestMap.App;

public class ProjectContext
{
    public ProjectModel Project { get; init; }
    public ILogger Logger => Project.Logger!;

    // Optional runtime-only state
    public string? RepoPath { get; set; }
    public string? CurrentCommit { get; set; }
    public TestBootstrapRuntimeState? TestBootstrapState { get; set; }

    public ProjectContext(
        ProjectModel project
    )
    {
        Project = project;
    }
}