using TestMap.App;

namespace TestMap.Services.RepoOperations;

public interface IRepoOperations
{
    Task CloneRepoAsync();
    Task DeleteRepoAsync();
}