using Serilog;
using TestMap.Models;
namespace TestMap.App;

public class ProjectContext
{
    public ProjectModel Project { get; init; }
    public ILogger Logger => Project.Logger!;

    // Optional runtime-only state
    public string? RepoPath { get; set; }
    public string? CurrentCommit { get; set; }

    public ProjectContext(
        ProjectModel project
    )
    {
        Project = project;
    }
}