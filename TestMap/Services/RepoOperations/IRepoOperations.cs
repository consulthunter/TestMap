using TestMap.App;

namespace TestMap.Services.RepoOperations;

public interface IRepoOperations
{
    Task PrepareRepositoryAsync(CancellationToken cancellationToken = default);
    Task DeleteRepoAsync();
}
