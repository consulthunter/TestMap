using TestMap.Services.Logging;

namespace TestMap.UnitTests.Services.Logging;

public sealed class ProjectLogDirectoryAllocationTests
{
    [Fact]
    public void Allocate_ResultCarriesOneConsistentReservation()
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var result = new ProjectLogDirectoryAllocator().Allocate(
                root,
                ProjectLogDirectoryTestData.RunStartedAtUtc,
                ProjectLogDirectoryTestData.Owner,
                ProjectLogDirectoryTestData.Repository,
                ProjectLogDirectoryTestData.ProjectId);

            Assert.Equal(ProjectLogDirectoryTestData.RunStartedAtUtc, result.RunStartedAtUtc);
            Assert.Equal(1, result.Ordinal);
            Assert.Equal(result.DirectoryPath, Path.GetDirectoryName(result.LogFilePath));
            Assert.Equal(result.DirectoryPath, Path.GetDirectoryName(result.ReservationMarkerPath));
            Assert.Equal(ProjectLogDirectoryAllocator.ReservationMarkerFileName, Path.GetFileName(result.ReservationMarkerPath));
        }
        finally { Directory.Delete(root, true); }
    }
}
