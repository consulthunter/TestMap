using TestMap.App;

namespace TestMap.Services.RepoOperations;

public interface IRepoOperations
{
    Task CloneRepoAsync(ProjectContext context);
    Task DeleteRepoAsync(ProjectContext context);
}