using TestMap.App;
using TestMap.Services.RepoOperations;
using TestMap.Models.Targets;

namespace TestMap.Services.RepoOperations;

public class RepoOperations : IRepoOperations
{
    private readonly ICloneRepoService _cloneService;
    private readonly IRepositoryMaterializationService _materializationService;
    private readonly ProjectContext _context;
    private readonly IDeleteProjectService _deleteService;


    public RepoOperations(
        ICloneRepoService cloneService,
        IRepositoryMaterializationService materializationService,
        IDeleteProjectService deleteService,
        ProjectContext context)
    {
        _cloneService = cloneService;
        _materializationService = materializationService;
        _deleteService = deleteService;
        _context = context;
    }

    public async Task PrepareRepositoryAsync(CancellationToken cancellationToken = default)
    {
        if (_context.RepositoryTarget is not null)
        {
            if (_context.MaterializedRevision?.Status != MaterializationStatus.Available)
                await _materializationService.MaterializeAsync(cancellationToken);
            return;
        }
        await _cloneService.CloneRepoAsync();
    }

    public async Task DeleteRepoAsync()
    {
        await _deleteService.DeleteProjectAsync();
    }
}
