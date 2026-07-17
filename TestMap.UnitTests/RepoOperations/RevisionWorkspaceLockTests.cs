using TestMap.Models.Targets;
using TestMap.Services.RepoOperations;

namespace TestMap.UnitTests.RepoOperations;

public sealed class RevisionWorkspaceLockTests
{
    [Fact]
    public async Task AcquireAsync_ContentionFailsAndReleaseAllowsNextOwner()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "testmap-lock-" + Guid.NewGuid().ToString("N"), "workspace");
        var service = new RevisionWorkspaceLock();
        var first = await service.AcquireAsync(workspace);
        try
        {
            var exception = await Assert.ThrowsAsync<RepositoryMaterializationException>(() => service.AcquireAsync(workspace));
            Assert.Equal(MaterializationStatus.WorkspaceBusy, exception.Status);
        }
        finally { await first.DisposeAsync(); }

        await using var second = await service.AcquireAsync(workspace);
    }

    [Fact]
    public async Task AcquireAsync_OrphanedUnlockedFile_IsSafelyReused()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "testmap-lock-" + Guid.NewGuid().ToString("N"), "workspace");
        Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);
        await File.WriteAllTextAsync(workspace + ".lock", "orphaned");
        await using (var handle = await new RevisionWorkspaceLock().AcquireAsync(workspace))
            Assert.True(File.Exists(workspace + ".lock"));
        Assert.False(File.Exists(workspace + ".lock"));
    }
}
