using TestMap.Services.Logging;

namespace TestMap.UnitTests.Services.Logging;

public sealed class ProjectLogDirectoryAllocatorTests
{
    [Fact]
    public void Allocate_UsesReadableUtcHierarchyAndExistingProjectIdFilename()
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var result = Allocate(root);

            Assert.Equal(1, result.Ordinal);
            Assert.EndsWith(
                Path.Combine("2026-07-16", "14-05-09_powershell-platyps"),
                result.DirectoryPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                ProjectLogDirectoryTestData.ProjectId + ".log",
                Path.GetFileName(result.LogFilePath));
            Assert.True(File.Exists(result.ReservationMarkerPath));
            Assert.DoesNotContain("482731", Path.GetFileName(result.DirectoryPath), StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Allocate_PreExistingDirectoriesAreUntouchedAndNextOrdinalIsSelected()
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var date = Path.Combine(root, "2026-07-16");
            var first = Path.Combine(date, "14-05-09_powershell-platyps");
            var second = first + "-02";
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            File.WriteAllText(Path.Combine(first, "historical.log"), "keep");

            var result = Allocate(root);

            Assert.Equal(3, result.Ordinal);
            Assert.EndsWith("-03", result.DirectoryPath, StringComparison.Ordinal);
            Assert.Equal("keep", File.ReadAllText(Path.Combine(first, "historical.log")));
            Assert.False(File.Exists(Path.Combine(first, ProjectLogDirectoryAllocator.ReservationMarkerFileName)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(2, "-02")]
    [InlineData(9, "-09")]
    [InlineData(10, "-10")]
    [InlineData(100, "-100")]
    public void Allocate_FormatsOrdinalsMonotonically(int desiredOrdinal, string expectedSuffix)
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var date = Path.Combine(root, "2026-07-16");
            Directory.CreateDirectory(date);
            var leaf = "14-05-09_powershell-platyps";
            for (var ordinal = 1; ordinal < desiredOrdinal; ordinal++)
            {
                var suffix = ordinal == 1 ? string.Empty : $"-{ordinal:D2}";
                Directory.CreateDirectory(Path.Combine(date, leaf + suffix));
            }

            var result = Allocate(root);

            Assert.Equal(desiredOrdinal, result.Ordinal);
            Assert.EndsWith(expectedSuffix, result.DirectoryPath, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Allocate_RejectsNonUtcAndUnsafeInputBeforeCreatingArtifacts()
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var allocator = new ProjectLogDirectoryAllocator();
            Assert.Throws<ArgumentException>(() => allocator.Allocate(
                root, ProjectLogDirectoryTestData.RunStartedAtUtc.ToOffset(TimeSpan.FromHours(-4)),
                "owner", "repository", "project"));
            Assert.Throws<ArgumentException>(() => allocator.Allocate(
                root, ProjectLogDirectoryTestData.RunStartedAtUtc,
                "../owner", "repository", "project"));
            Assert.Throws<ArgumentException>(() => allocator.Allocate(
                string.Empty, ProjectLogDirectoryTestData.RunStartedAtUtc,
                "owner", "repository", "project"));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("2026-07-16T23:59:59+00:00", "2026-07-16", "23-59-59_owner-repository")]
    [InlineData("2026-07-17T00:00:00+00:00", "2026-07-17", "00-00-00_owner-repository")]
    public void Allocate_DateAndTimeComeFromOneUtcInstant(
        string timestamp,
        string expectedDate,
        string expectedLeaf)
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var result = new ProjectLogDirectoryAllocator().Allocate(
                root,
                DateTimeOffset.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture),
                "owner", "repository", "project");

            Assert.EndsWith(Path.Combine(expectedDate, expectedLeaf), result.DirectoryPath, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Allocate_OneHundredConcurrentRunsOwnDistinctDirectories()
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var allocator = new ProjectLogDirectoryAllocator();
            var started = System.Diagnostics.Stopwatch.StartNew();
            var results = await Task.WhenAll(Enumerable.Range(1, 100).Select(index => Task.Run(() =>
                allocator.Allocate(
                    root,
                    ProjectLogDirectoryTestData.RunStartedAtUtc,
                    ProjectLogDirectoryTestData.Owner,
                    ProjectLogDirectoryTestData.Repository,
                    $"{index:D3}_PowerShell-PlatyPS"))));
            started.Stop();

            Assert.Equal(100, results.Select(result => result.DirectoryPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(Enumerable.Range(1, 100), results.Select(result => result.Ordinal).Order());
            Assert.All(results, result => Assert.True(File.Exists(result.ReservationMarkerPath)));
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(10), $"Allocation took {started.Elapsed}.");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Allocate_PartialReservedRunRemainsVisibleAndIsNotReused()
    {
        var root = ProjectLogDirectoryTestData.CreateRoot();
        try
        {
            var first = Allocate(root);
            Assert.False(File.Exists(first.LogFilePath));

            var second = new ProjectLogDirectoryAllocator().Allocate(
                root,
                ProjectLogDirectoryTestData.RunStartedAtUtc,
                ProjectLogDirectoryTestData.Owner,
                ProjectLogDirectoryTestData.Repository,
                "901245_PowerShell-PlatyPS");

            Assert.Equal(2, second.Ordinal);
            Assert.True(File.Exists(first.ReservationMarkerPath));
            Assert.True(Directory.Exists(first.DirectoryPath));
            Assert.NotEqual(first.DirectoryPath, second.DirectoryPath);
        }
        finally { Directory.Delete(root, true); }
    }

    private static ProjectLogDirectoryAllocation Allocate(string root) =>
        new ProjectLogDirectoryAllocator().Allocate(
            root,
            ProjectLogDirectoryTestData.RunStartedAtUtc,
            ProjectLogDirectoryTestData.Owner,
            ProjectLogDirectoryTestData.Repository,
            ProjectLogDirectoryTestData.ProjectId);
}
