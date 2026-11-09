namespace TestMap.Services.Targets;

public interface IAtomicFilePublisher
{
    Task PublishAsync(string destinationPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
}

public sealed class AtomicFilePublisher : IAtomicFilePublisher
{
    public async Task PublishAsync(string destinationPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Destination has no directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
