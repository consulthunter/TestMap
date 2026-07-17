using TestMap.Services.Logging;

namespace TestMap.UnitTests.Services.Logging;

public sealed class ProjectLogDirectoryAllocatorConcurrencyTests
{
    [Fact]
    public async Task Allocate_ConcurrentContendersHaveExactlyOneOwnerPerOrdinal()
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var results = await Task.WhenAll(Enumerable.Range(1, 25).Select(index => Task.Run(() =>
                new ProjectLogDirectoryAllocator().Allocate(
                    root,
                    ProjectLogDirectoryTestData.RunStartedAtUtc,
                    ProjectLogDirectoryTestData.Owner,
                    ProjectLogDirectoryTestData.Repository,
                    $"concurrent-{index:D2}"))));

            Assert.Equal(25, results.Select(result => result.DirectoryPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(25, Directory.EnumerateFiles(
                root,
                ProjectLogDirectoryAllocator.ReservationMarkerFileName,
                SearchOption.AllDirectories).Count());
        }
        finally { Directory.Delete(root, true); }
    }
}
