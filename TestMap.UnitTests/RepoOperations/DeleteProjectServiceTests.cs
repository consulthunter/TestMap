using TestMap.App;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Runtime;
using TestMap.Services.RepoOperations;

namespace TestMap.UnitTests.RepoOperations;

public sealed class DeleteProjectServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteProjectAsync_KeepFilesFalse_DeletesRevisionAndPrunesEmptyRepositoryParents()
    {
        var tempRoot = Directory.CreateDirectory(Path.Combine(_root, "Temp")).FullName;
        var workspace = Directory.CreateDirectory(
            Path.Combine(tempRoot, "owner", "repository", new string('a', 40))).FullName;
        await File.WriteAllTextAsync(Path.Combine(workspace, "source.cs"), "source");
        var service = CreateService(tempRoot, workspace, keepProjectFiles: false);

        await service.DeleteProjectAsync();

        Assert.False(Directory.Exists(workspace));
        Assert.False(Directory.Exists(Path.Combine(tempRoot, "owner", "repository")));
        Assert.False(Directory.Exists(Path.Combine(tempRoot, "owner")));
        Assert.True(Directory.Exists(tempRoot));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteProjectAsync_KeepFilesTrue_PreservesWorkspace()
    {
        var tempRoot = Directory.CreateDirectory(Path.Combine(_root, "Temp")).FullName;
        var workspace = Directory.CreateDirectory(Path.Combine(tempRoot, "repository")).FullName;
        var service = CreateService(tempRoot, workspace, keepProjectFiles: true);

        await service.DeleteProjectAsync();

        Assert.True(Directory.Exists(workspace));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteProjectAsync_WorkspaceOutsideTempRoot_RefusesDeletion()
    {
        var tempRoot = Directory.CreateDirectory(Path.Combine(_root, "Temp")).FullName;
        var workspace = Directory.CreateDirectory(Path.Combine(_root, "Outside", "repository")).FullName;
        var service = CreateService(tempRoot, workspace, keepProjectFiles: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteProjectAsync());

        Assert.Contains("outside the configured temporary root", exception.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(workspace));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteProjectAsync_SharedOwner_PreservesSiblingRepository()
    {
        var tempRoot = Directory.CreateDirectory(Path.Combine(_root, "Temp")).FullName;
        var workspace = Directory.CreateDirectory(
            Path.Combine(tempRoot, "owner", "repository-a", new string('a', 40))).FullName;
        var sibling = Directory.CreateDirectory(
            Path.Combine(tempRoot, "owner", "repository-b", new string('b', 40))).FullName;
        await File.WriteAllTextAsync(Path.Combine(sibling, "source.cs"), "source");
        var service = CreateService(tempRoot, workspace, keepProjectFiles: false);

        await service.DeleteProjectAsync();

        Assert.False(Directory.Exists(workspace));
        Assert.True(Directory.Exists(sibling));
        Assert.True(Directory.Exists(Path.Combine(tempRoot, "owner")));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    private static DeleteProjectService CreateService(string tempRoot, string workspace, bool keepProjectFiles)
    {
        var config = new TestMapConfig
        {
            RuntimeConfig = new RuntimeConfig
            {
                Project = new ProjectConfig { KeepProjectFiles = keepProjectFiles },
                FilePaths = new FilePathConfig { TempDirPath = tempRoot }
            }
        };
        var project = new ProjectModel(
            directoryPath: workspace,
            tempDirPath: tempRoot,
            config: config);
        return new DeleteProjectService(new ProjectContext(project));
    }
}
