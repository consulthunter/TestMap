using LibGit2Sharp;
using TestMap.App;
using TestMap.Models;
using TestMap.Services.TestGeneration.Workspace;
using TestMap.Models.Targets;
using TestMap.Services.Targets;

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

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RollbackChangesAsync_HeadMovedAfterAttempt_RestoresPinnedBaseCommit()
    {
        var repoPath = CreateRepository();
        string baseCommit;
        string movedCommit;
        using (var repo = new Repository(repoPath))
        {
            baseCommit = repo.Head.Tip.Sha;
            File.WriteAllText(Path.Combine(repoPath, "second.txt"), "second");
            Commands.Stage(repo, "second.txt");
            var signature = new Signature("TestMap", "testmap@example.com", DateTimeOffset.UtcNow);
            movedCommit = repo.Commit("Moved head", signature, signature).Sha;
        }
        Assert.NotEqual(baseCommit, movedCommit);
        var target = new TargetIdentityService().Create("owner/repository", baseCommit, [1]);
        var project = new ProjectModel(directoryPath: repoPath);
        project.BindTarget(target);
        project.MaterializedRevision = new MaterializedRevision(
            target.TargetId, target.Repository, baseCommit, baseCommit, target.Url,
            new TargetPaths(repoPath, "db", "artifacts", "log"), new string('a', 64), new string('b', 64),
            DateTimeOffset.UtcNow, MaterializationStatus.Available);

        await new RollbackWorkspaceService(new ProjectContext(project)).RollbackChangesAsync();

        using var restored = new Repository(repoPath);
        Assert.Equal(baseCommit, restored.Head.Tip.Sha);
        Assert.False(File.Exists(Path.Combine(repoPath, "second.txt")));
        Assert.False(restored.RetrieveStatus().IsDirty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RollbackChangesAsync_DeletedGitMetadata_FailsRestore()
    {
        var repoPath = CreateRepository();
        string commit;
        using (var repo = new Repository(repoPath)) commit = repo.Head.Tip.Sha;
        var target = new TargetIdentityService().Create("owner/repository", commit, [1]);
        var project = new ProjectModel(directoryPath: repoPath);
        project.BindTarget(target);
        project.MaterializedRevision = new MaterializedRevision(target.TargetId, target.Repository, commit, commit, target.Url,
            new TargetPaths(repoPath, "db", "artifacts", "log"), new string('a', 64), new string('b', 64),
            DateTimeOffset.UtcNow, MaterializationStatus.Available);
        var gitPath = Path.Combine(repoPath, ".git");
        foreach (var file in Directory.EnumerateFiles(gitPath, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(gitPath, true);
        await Assert.ThrowsAnyAsync<Exception>(() => new RollbackWorkspaceService(new ProjectContext(project)).RollbackChangesAsync());
    }

    /// <summary>
    /// A checkout made under core.autocrlf=true (Git for Windows' system default) holds CRLF
    /// files that the host reports clean but a Linux container reports modified. Rollback must
    /// pin autocrlf=false and leave the committed bytes on disk.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RollbackChangesAsync_CrlfCheckout_RestoresCommittedLineEndings()
    {
        var repoPath = CreateRepository();
        var lfFile = Path.Combine(repoPath, "lines.txt");
        using (var repo = new Repository(repoPath))
        {
            await File.WriteAllTextAsync(lfFile, "a\nb\n");
            Commands.Stage(repo, "lines.txt");
            var signature = new Signature("TestMap", "testmap@example.com", DateTimeOffset.UtcNow);
            repo.Commit("LF file", signature, signature);

            repo.Config.Set("core.autocrlf", "true", ConfigurationLevel.Local);
            File.Delete(lfFile);
            repo.CheckoutPaths(repo.Head.Tip.Sha, ["lines.txt"], new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
        }
        Assert.Equal("a\r\nb\r\n", await File.ReadAllTextAsync(lfFile));

        var service = new RollbackWorkspaceService(
            new ProjectContext(new ProjectModel(directoryPath: repoPath)));

        await service.RollbackChangesAsync();

        Assert.Equal("a\nb\n", await File.ReadAllTextAsync(lfFile));
        using var restored = new Repository(repoPath);
        Assert.Equal("false", restored.Config.Get<string>("core.autocrlf", ConfigurationLevel.Local)?.Value);
        Assert.False(restored.RetrieveStatus().IsDirty);
    }

    /// <summary>
    /// With <c>* text=auto</c> and only core.autocrlf pinned, a Windows checkout writes CRLF (the
    /// native eol). An agent in the Linux container that rewrites a file unchanged saves it with
    /// LF, which reads as modified, and a forced checkout plus hard reset cannot clear it because
    /// the normalized content already matches. Pinning core.eol=lf checks the tree out as the
    /// committed LF bytes, so the same rewrite stays clean.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RollbackChangesAsync_TextAutoWorkspace_PinsLfAndClearsContainerRewrite()
    {
        var repoPath = CreateTextAutoRepository(out var sourceFile);
        CheckOutAsWorkspacesDidBeforeEolPin(repoPath);
        Assert.Equal("line one\r\nline two\r\n", await File.ReadAllTextAsync(sourceFile));
        RewriteAsContainerDoes(sourceFile, "\n");
        using (var repo = new Repository(repoPath))
            Assert.True(repo.RetrieveStatus().IsDirty);

        var service = new RollbackWorkspaceService(
            new ProjectContext(new ProjectModel(directoryPath: repoPath)));
        await service.RollbackChangesAsync();

        using (var restored = new Repository(repoPath))
        {
            Assert.Equal("lf", restored.Config.Get<string>("core.eol", ConfigurationLevel.Local)?.Value);
            Assert.Equal("false", restored.Config.Get<string>("core.autocrlf", ConfigurationLevel.Local)?.Value);
            Assert.False(restored.RetrieveStatus().IsDirty);
        }
        Assert.Equal("line one\nline two\n", await File.ReadAllTextAsync(sourceFile));

        RewriteAsContainerDoes(sourceFile, "\n");
        using var rewritten = new Repository(repoPath);
        Assert.False(rewritten.RetrieveStatus().IsDirty);
    }

    /// <summary>
    /// When a forced checkout plus hard reset still leaves a path that only differs by line
    /// endings, rollback rewrites the working tree from the commit instead of failing the run.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RollbackChangesAsync_LineEndingOnlyChangeSurvivesReset_RewritesWorkingTree()
    {
        var repoPath = CreateTextAutoRepository(out var sourceFile);
        var service = new RollbackWorkspaceService(
            new ProjectContext(new ProjectModel(directoryPath: repoPath)));
        await service.RollbackChangesAsync();

        RewriteAsContainerDoes(sourceFile, "\r\n");
        using (var repo = new Repository(repoPath))
            Assert.True(repo.RetrieveStatus().IsDirty);

        await service.RollbackChangesAsync();

        using var restored = new Repository(repoPath);
        Assert.False(restored.RetrieveStatus().IsDirty);
        Assert.Equal("line one\nline two\n", await File.ReadAllTextAsync(sourceFile));
    }

    private string CreateTextAutoRepository(out string sourceFile)
    {
        var repoPath = CreateRepository();
        sourceFile = Path.Combine(repoPath, "src", "Builder.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(Path.Combine(repoPath, ".gitattributes"), "* text=auto\n");
        File.WriteAllText(sourceFile, "line one\nline two\n");

        using var repo = new Repository(repoPath);
        Commands.Stage(repo, [".gitattributes", "src/Builder.cs"]);
        var signature = new Signature("TestMap", "testmap@example.com", DateTimeOffset.UtcNow);
        repo.Commit("text=auto source", signature, signature);
        return repoPath;
    }

    /// <summary>Pins core.autocrlf=false with core.eol unset and rewrites the tree, as the
    /// materializer did before core.eol was pinned.</summary>
    private static void CheckOutAsWorkspacesDidBeforeEolPin(string repoPath)
    {
        string sha;
        using (var repo = new Repository(repoPath))
        {
            repo.Config.Set("core.autocrlf", "false", ConfigurationLevel.Local);
            repo.Config.Set("core.eol", "crlf", ConfigurationLevel.Local);
            sha = repo.Head.Tip.Sha;
            File.Delete(Path.Combine(repo.Info.Path, "index"));
        }
        using (var repo = new Repository(repoPath))
        {
            repo.Reset(ResetMode.Hard, repo.Lookup<Commit>(sha));
            // Windows' native eol is CRLF; unset the explicit value so only autocrlf is pinned.
            repo.Config.Unset("core.eol", ConfigurationLevel.Local);
        }
    }

    /// <summary>Saves the file's text unchanged with the given line ending and a later mtime,
    /// as an agent's whole-file edit or mutate-and-restore script does.</summary>
    private static void RewriteAsContainerDoes(string path, string newline)
    {
        var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
        File.WriteAllText(path, string.Join(newline, lines));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
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
