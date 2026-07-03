using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Models.Code;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="ToolAttemptGeneratedTestService"/> covering path normalisation,
/// file-to-member matching, and the linking result.
/// </summary>
public sealed class ToolAttemptGeneratedTestServiceTests
{
    // ─── BuildAbsolutePaths (static / pure) ───────────────────────────────────

    /// <summary>
    /// Relative paths are combined with the workspace root and fully normalised.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildAbsolutePaths_RelativePaths_CombinesWithWorkspace()
    {
        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "repo"));
        var changed = new[] { "src/Tests/FooTests.cs", "src/Bar.cs" };

        var result = ToolAttemptGeneratedTestService.BuildAbsolutePaths(workspace, changed);

        Assert.Equal(2, result.Count);
        Assert.Contains(Path.GetFullPath(Path.Combine(workspace, "src", "Tests", "FooTests.cs")), result);
        Assert.Contains(Path.GetFullPath(Path.Combine(workspace, "src", "Bar.cs")), result);
    }

    /// <summary>
    /// Container-absolute paths starting with /workspace/ are stripped before combining.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildAbsolutePaths_ContainerAbsolutePaths_StripsWorkspacePrefix()
    {
        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "repo"));
        var changed = new[] { "/workspace/src/Tests/FooTests.cs" };

        var result = ToolAttemptGeneratedTestService.BuildAbsolutePaths(workspace, changed);

        Assert.Single(result);
        Assert.Contains(
            Path.GetFullPath(Path.Combine(workspace, "src", "Tests", "FooTests.cs")),
            result);
    }

    /// <summary>
    /// Empty and whitespace-only entries are silently ignored.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildAbsolutePaths_EmptyOrWhitespaceEntries_IgnoresThem()
    {
        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "repo"));
        var changed = new[] { "", "  ", "src/Foo.cs" };

        var result = ToolAttemptGeneratedTestService.BuildAbsolutePaths(workspace, changed);

        Assert.Single(result);
    }

    /// <summary>
    /// Empty workspace path returns an empty set rather than throwing.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildAbsolutePaths_EmptyWorkspace_ReturnsEmpty()
    {
        var result = ToolAttemptGeneratedTestService.BuildAbsolutePaths(
            string.Empty, ["src/Foo.cs"]);
        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseAddedLines_TracksOnlyNewFileLines()
    {
        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "PatchRepo"));
        var result = ToolAttemptGeneratedTestService.ParseAddedLines(
            [
                "diff --git a/Tests/FooTests.cs b/Tests/FooTests.cs",
                "--- a/Tests/FooTests.cs",
                "+++ b/Tests/FooTests.cs",
                "@@ -1,2 +1,4 @@",
                " existing",
                "+[Fact]",
                "+public void AddedTest()",
                " trailing"
            ],
            workspace);

        var path = Path.GetFullPath(Path.Combine(workspace, "Tests", "FooTests.cs"));
        Assert.Equal([1, 2], result[path].OrderBy(x => x));
    }

    // ─── LinkAsync integration (in-memory DB) ─────────────────────────────────

    /// <summary>
    /// When the changed file contains a test member, one linking row is inserted.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task LinkAsync_ChangedFileHasTestMember_InsertsLinkRow()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (experimentRunId, candidateId) = await SeedExperimentGraphAsync(db);
        var attemptId = await SeedAttemptAsync(db, experimentRunId, candidateId);

        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "LinkRepo"));
        var testFilePath = Path.GetFullPath(Path.Combine(workspace, "Tests", "FooTests.cs"));
        await SeedCodeGraphAsync(db, testFilePath, isTestMember: true, startLine: 1);
        var artifactPath = CreatePatchArtifact(
            workspace,
            "Tests/FooTests.cs",
            """
             existing
            +[Fact]
            +public void TestFoo()
             trailing
            """);

        var service = new ToolAttemptGeneratedTestService(
            db, new ToolAttemptGeneratedTestRepository(db));
        var attempt = new ToolAttempt
        {
            Id = attemptId,
            WorkspacePath = workspace,
            ArtifactPath = artifactPath
        };

        // Act
        var result = await service.LinkAsync(
            attempt,
            ["Tests/FooTests.cs"],
            projectId: 1);

        // Assert
        Assert.Equal(1, result.LinkedCount);
        Assert.Equal(1, await db.ToolAttemptGeneratedTests.CountAsync());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LinkAsync_PreExistingTestInChangedFile_DoesNotLinkIt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (experimentRunId, candidateId) = await SeedExperimentGraphAsync(db);
        var attemptId = await SeedAttemptAsync(db, experimentRunId, candidateId);

        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "FilteredRepo"));
        var testFilePath = Path.GetFullPath(Path.Combine(workspace, "Tests", "FooTests.cs"));
        await SeedCodeGraphAsync(db, testFilePath, isTestMember: true, startLine: 1);
        var testObjectId = await db.Objects.Select(x => x.Id).SingleAsync();
        db.Members.Add(new MemberEntity
        {
            ObjectEntityId = testObjectId,
            Name = "ExistingTest",
            Kind = "method",
            IsTestMember = true,
            Location = new Location(10, 0, 12, 0)
        });
        await db.SaveChangesAsync();

        var artifactPath = CreatePatchArtifact(
            workspace,
            "Tests/FooTests.cs",
            """
             existing
            +[Fact]
            +public void TestFoo()
             trailing
            """);
        var service = new ToolAttemptGeneratedTestService(
            db, new ToolAttemptGeneratedTestRepository(db));

        var result = await service.LinkAsync(
            new ToolAttempt
            {
                Id = attemptId,
                WorkspacePath = workspace,
                ArtifactPath = artifactPath
            },
            ["Tests/FooTests.cs"],
            projectId: 1);

        Assert.Single(result.LinkedMemberIds);
        var linkedMember = await db.Members.SingleAsync(x => result.LinkedMemberIds.Contains(x.Id));
        Assert.Equal("TestFoo", linkedMember.Name);
    }

    /// <summary>
    /// Non-test members in changed files are not linked.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task LinkAsync_ChangedFileHasOnlyProductionMember_NoLinkInserted()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (experimentRunId, candidateId) = await SeedExperimentGraphAsync(db);
        var attemptId = await SeedAttemptAsync(db, experimentRunId, candidateId);

        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "ProdRepo"));
        var srcFilePath = Path.GetFullPath(Path.Combine(workspace, "Src", "Calculator.cs"));
        await SeedCodeGraphAsync(db, srcFilePath, isTestMember: false);

        var service = new ToolAttemptGeneratedTestService(
            db, new ToolAttemptGeneratedTestRepository(db));
        var attempt = new ToolAttempt
        {
            Id = attemptId,
            WorkspacePath = workspace,
            ArtifactPath = string.Empty
        };

        // Act
        var result = await service.LinkAsync(attempt, ["Src/Calculator.cs"], projectId: 1);

        // Assert
        Assert.Equal(0, result.LinkedCount);
        Assert.Equal(0, await db.ToolAttemptGeneratedTests.CountAsync());
    }

    /// <summary>
    /// Empty changed-files list returns immediately with zero linked count.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task LinkAsync_EmptyChangedFiles_ReturnsZero()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var attemptId = await SeedAttemptAsync(db, runId, candidateId);
        var service = new ToolAttemptGeneratedTestService(
            db, new ToolAttemptGeneratedTestRepository(db));

        // Act
        var result = await service.LinkAsync(
            new ToolAttempt { Id = attemptId, WorkspacePath = "/repo", ArtifactPath = string.Empty },
            [],
            projectId: 1);

        // Assert
        Assert.Equal(0, result.LinkedCount);
        Assert.Equal(0, await db.ToolAttemptGeneratedTests.CountAsync());
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<(int ExperimentRunId, int CandidateMethodId)> SeedExperimentGraphAsync(
        TestMapDbContext db)
    {
        var run = new ExperimentRunEntity
        {
            ProjectId = 1,
            StartTime = DateTime.UtcNow,
            Objective = "TestSuiteExpansion",
            CandidateSelectionStrategy = "Existing",
            Configuration = "{}",
            ResultsFilePath = string.Empty,
            Status = "Running"
        };
        db.ExperimentRuns.Add(run);
        await db.SaveChangesAsync();

        var candidate = new CandidateMethodEntity
        {
            ExperimentRunId = run.Id,
            SourceMemberId = 1,
            SourceMethodName = "Calculate",
            SourceMethodSignature = "public int Calculate()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();
        return (run.Id, candidate.Id);
    }

    private static async Task<int> SeedAttemptAsync(
        TestMapDbContext db, int experimentRunId, int candidateMethodId)
    {
        var repo = new ToolAttemptRepository(db);
        return await repo.InsertAsync(new ToolAttempt
        {
            ExperimentRunId = experimentRunId,
            CandidateMethodId = candidateMethodId,
            ToolId = "codex",
            RunStatus = ToolRunStatus.Completed,
            StartedAt = DateTime.UtcNow,
            TimeoutSeconds = 2700
        });
    }

    private static async Task SeedCodeGraphAsync(
        TestMapDbContext db,
        string filePath,
        bool isTestMember,
        int startLine = 0)
    {
        var csproj = new CSharpProjectEntity
        {
            FilePath = Path.GetDirectoryName(filePath) + ".csproj",
            SolutionId = 0
        };
        db.CSharpProjects.Add(csproj);
        await db.SaveChangesAsync();

        var file = new FileEntity
        {
            CSharpProjectId = csproj.Id,
            FilePath = filePath
        };
        db.Files.Add(file);
        await db.SaveChangesAsync();

        var obj = new ObjectEntity
        {
            FileId = file.Id,
            Name = isTestMember ? "FooTests" : "Calculator",
            Kind = "class",
            IsTestObject = isTestMember
        };
        db.Objects.Add(obj);
        await db.SaveChangesAsync();

        var member = new MemberEntity
        {
            ObjectEntityId = obj.Id,
            Name = isTestMember ? "TestFoo" : "Calculate",
            Kind = "method",
            IsTestMember = isTestMember,
            Location = new Location(startLine, 0, startLine + 2, 0)
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
    }

    private static string CreatePatchArtifact(
        string workspace,
        string relativePath,
        string hunkLines)
    {
        var artifactPath = Path.Combine(
            Path.GetTempPath(),
            $"testmap-patch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactPath);
        var normalizedPath = relativePath.Replace('\\', '/');
        File.WriteAllText(
            Path.Combine(artifactPath, "patch.diff"),
            string.Join(
                Environment.NewLine,
                $"diff --git a/{normalizedPath} b/{normalizedPath}",
                $"--- a/{normalizedPath}",
                $"+++ b/{normalizedPath}",
                "@@ -1,2 +1,4 @@",
                hunkLines));
        return artifactPath;
    }
}
