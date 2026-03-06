

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

    public async Task CloneRepoAsync()
    {
        await _cloneService.CloneRepoAsync();
    }

    public async Task DeleteRepoAsync()
    {
        await _deleteService.DeleteProjectAsync();
    }
}