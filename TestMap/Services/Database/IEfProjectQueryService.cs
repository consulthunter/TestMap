namespace TestMap.Services.Database;

public interface IEfProjectQueryService
{
    Task<int> GetProjectCountAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetProjectNamesAsync(CancellationToken cancellationToken = default);
}