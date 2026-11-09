using TestMap.Models.Targets;

namespace TestMap.Services.RepoOperations;

public interface IRevisionWorkspaceLock
{
    Task<IAsyncDisposable> AcquireAsync(string workspacePath, CancellationToken cancellationToken = default);
}

public sealed class RevisionWorkspaceLock : IRevisionWorkspaceLock
{
    public async Task<IAsyncDisposable> AcquireAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var parent = Directory.GetParent(workspacePath)?.FullName ?? throw new InvalidDataException("Workspace has no parent directory.");
        Directory.CreateDirectory(parent);
        var path = workspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".lock";
        try
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous);
            stream.SetLength(0);
            var payload = System.Text.Encoding.UTF8.GetBytes($"pid={Environment.ProcessId};created={DateTimeOffset.UtcNow:O}\n");
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return new Handle(path, stream);
        }
        catch (IOException exception)
        {
            throw new RepositoryMaterializationException(MaterializationStatus.WorkspaceBusy, "The revision workspace is already in use.", exception);
        }
    }

    private sealed class Handle(string path, FileStream stream) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            if (File.Exists(path)) File.Delete(path);
            return ValueTask.CompletedTask;
        }
    }
}
