

using TestMap.App;
using TestMap.Services.RepoOperations.Delete;
using TestMap.Services.RepoOperations.Clone;

namespace TestMap.Services.RepoOperations;

public class RepoOperations : IRepoOperations
{
    private readonly ICloneRepoService _cloneService;
    private readonly IDeleteProjectService _deleteService;


    public RepoOperations(
        ICloneRepoService cloneService,
        IDeleteProjectService deleteService)
    {
        _cloneService = cloneService;
        _deleteService = deleteService;
    }

    public async Task CloneRepoAsync(ProjectContext context)
    {
        await _cloneService.CloneRepoAsync();
        context.Logger?.Information("Repo cloned to {RepoPath}", context.RepoPath);
    }

    public async Task DeleteRepoAsync(ProjectContext context)
    {
        await _deleteService.DeleteProjectAsync();
        context.Logger?.Information("Repo deleted: {RepoId}", context.Project.ProjectId);
    }
}