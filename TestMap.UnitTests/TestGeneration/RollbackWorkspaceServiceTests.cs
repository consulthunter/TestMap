using LibGit2Sharp;
using TestMap.App;
using TestMap.Models;
using TestMap.Services.TestGeneration.Workspace;

namespace TestMap.UnitTests.TestGeneration;

public sealed class RollbackWorkspaceServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RollbackChangesAsync_DeletesUntrackedFilesAndDirectories()
    {
        var repoPath = CreateRepository();
        var trackedFile = Path.Combine(repoPath, "tracked.txt");
        await File.WriteAllTextAsync(trackedFile, "changed");

        var untrackedDirectory = Path.Combine(repoPath, "generated", "nested");
        Directory.CreateDirectory(untrackedDirectory);
        var untrackedFile = Path.Combine(untrackedDirectory, "new-test.cs");
        await File.WriteAllTextAsync(untrackedFile, "generated");

        var service = new RollbackWorkspaceService(
            new ProjectContext(new ProjectModel(directoryPath: repoPath)));

        await service.RollbackChangesAsync();

        Assert.Equal("original", await File.ReadAllTextAsync(trackedFile));
        Assert.False(File.Exists(untrackedFile));
        Assert.False(Directory.Exists(Path.Combine(repoPath, "generated")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RollbackChangesAsync_DeletesIgnoredBinAndObjDirectories()
    {
        var repoPath = CreateRepository();
        var projectDirectory = Path.Combine(repoPath, "src", "Sample");
        var binDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "bin", "Debug")).FullName;
        var objDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "obj", "Debug")).FullName;
        await File.WriteAllTextAsync(Path.Combine(binDirectory, "artifact.dll"), "binary");
        await File.WriteAllTextAsync(Path.Combine(objDirectory, "generated.g.cs"), "generated");

        var service = new RollbackWorkspaceService(
            new ProjectContext(new ProjectModel(directoryPath: repoPath)));

        await service.RollbackChangesAsync();

        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "bin")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
    }

    private string CreateRepository()
    {
        var repoPath = Path.Combine(Path.GetTempPath(), $"testmap-rollback-{Guid.NewGuid():N}");
        _directoriesToDelete.Add(repoPath);
        Directory.CreateDirectory(repoPath);
        Repository.Init(repoPath);

        var trackedFile = Path.Combine(repoPath, "tracked.txt");
        File.WriteAllText(trackedFile, "original");

        using var repo = new Repository(repoPath);
        Commands.Stage(repo, trackedFile);

        var signature = new Signature("TestMap", "testmap@example.com", DateTimeOffset.UtcNow);
        repo.Commit("Initial commit", signature, signature);

        return repoPath;
    }

    public void Dispose()
    {
        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);

                Directory.Delete(directory, true);
            }
        }
    }
}
