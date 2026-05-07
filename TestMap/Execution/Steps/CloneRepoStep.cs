using TestMap.App;
using TestMap.Services.RepoOperations;

namespace TestMap.Execution.Steps;

public class CloneRepoStep : IPipelineStep
{
    private readonly IRepoOperations _repoOps;

    public CloneRepoStep(IRepoOperations repoOps)
    {
        _repoOps = repoOps;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _repoOps.CloneRepoAsync();
    }
}