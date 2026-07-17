using TestMap.Services.Logging;

namespace TestMap.UnitTests.Services.Logging;

public sealed class ProjectLogDirectoryAllocatorFailureTests
{
    [Fact]
    public void Allocate_ReservedDirectoryWithoutLogIsRetainedAsPartialEvidence()
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var allocator = new ProjectLogDirectoryAllocator();
            var partial = allocator.Allocate(
                root,
                ProjectLogDirectoryTestData.RunStartedAtUtc,
                ProjectLogDirectoryTestData.Owner,
                ProjectLogDirectoryTestData.Repository,
                "partial-run");

            var later = allocator.Allocate(
                root,
                ProjectLogDirectoryTestData.RunStartedAtUtc,
                ProjectLogDirectoryTestData.Owner,
                ProjectLogDirectoryTestData.Repository,
                "later-run");

            Assert.True(File.Exists(partial.ReservationMarkerPath));
            Assert.False(File.Exists(partial.LogFilePath));
            Assert.Equal(2, later.Ordinal);
            Assert.True(Directory.Exists(partial.DirectoryPath));
        }
        finally { Directory.Delete(root, true); }
    }
}
