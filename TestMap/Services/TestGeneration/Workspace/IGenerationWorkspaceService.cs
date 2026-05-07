namespace TestMap.Services.TestGeneration.Workspace;

public interface IGenerationWorkspaceService
{
    Task EnsureWorkspaceReadyAsync(CancellationToken cancellationToken = default);

    Task RollbackChangesAsync(CancellationToken cancellationToken = default);

    Task PersistAcceptedChangesAsync(string message, CancellationToken cancellationToken = default);
}