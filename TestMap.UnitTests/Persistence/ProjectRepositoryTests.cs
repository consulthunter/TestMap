using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="ProjectRepository"/> using an in-memory SQLite database.
/// Verifies insert/update semantics, the ContentHash uniqueness contract, and that
/// the mapping fix (DbId populated on read-back) is exercised end-to-end.
/// </summary>
public sealed class ProjectRepositoryTests
{
    /// <summary>
    /// Inserting a brand-new project assigns a positive database ID and sets DbId on
    /// the domain model as a side-effect.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_NewProject_AssignsDbId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ProjectRepository(db);
        var model = MakeProject("acme", "widget");

        var id = await repo.InsertOrUpdateAsync(model);

        Assert.True(id > 0);
        Assert.Equal(id, model.DbId);
        Assert.Equal(1, await db.Projects.CountAsync());
    }

    /// <summary>
    /// Calling InsertOrUpdateAsync with the same ContentHash twice does not create a
    /// duplicate row — the second call returns the existing row's ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_SameContentHash_ReturnsExistingId_WithoutDuplicate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ProjectRepository(db);
        var model = MakeProject("org", "repo");

        var firstId = await repo.InsertOrUpdateAsync(model);
        var secondId = await repo.InsertOrUpdateAsync(model);

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await db.Projects.CountAsync());
    }

    /// <summary>
    /// When a project with a matching ContentHash already exists but a tracked field
    /// has changed, InsertOrUpdateAsync updates the row and returns the same ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_ChangedBranch_UpdatesRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ProjectRepository(db);
        var model = MakeProject("org", "svc");
        await repo.InsertOrUpdateAsync(model);

        model.Branch = "feature/new";
        var updatedId = await repo.InsertOrUpdateAsync(model);

        var entity = await db.Projects.FirstAsync();
        Assert.Equal(model.DbId, updatedId);
        Assert.Equal("feature/new", entity.Branch);
        Assert.Equal(1, await db.Projects.CountAsync());
    }

    /// <summary>
    /// GetAllAsync returns domain models with DbId populated — previously the mapping
    /// only populated GitHubUrl, leaving DbId as 0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_ReturnsMappedProjectsWithDbId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ProjectRepository(db);
        await repo.InsertOrUpdateAsync(MakeProject("org", "alpha"));
        await repo.InsertOrUpdateAsync(MakeProject("org", "beta"));

        var projects = await repo.GetAllAsync();

        Assert.Equal(2, projects.Count);
        Assert.All(projects, p => Assert.True(p.DbId > 0));
        Assert.All(projects, p => Assert.Equal("org", p.Owner));
    }

    /// <summary>
    /// GetByIdAsync returns a domain model with DbId and Owner correctly populated.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_ReturnsMappedProjectWithDbId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ProjectRepository(db);
        var inserted = MakeProject("myorg", "myrepo");
        var id = await repo.InsertOrUpdateAsync(inserted);

        var retrieved = await repo.GetByIdAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved!.DbId);
        Assert.Equal("myorg", retrieved.Owner);
        Assert.Equal("myrepo", retrieved.RepoName);
    }

    /// <summary>
    /// DeleteAsync removes the project row so subsequent GetAllAsync returns an empty list.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_RemovesProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ProjectRepository(db);
        var id = await repo.InsertOrUpdateAsync(MakeProject("bye", "gone"));

        await repo.DeleteAsync(id);

        Assert.Empty(await repo.GetAllAsync());
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ProjectModel MakeProject(string owner, string repoName) =>
        new(gitHubUrl: $"https://github.com/{owner}/{repoName}",
            owner: owner,
            repoName: repoName,
            directoryPath: $"/repos/{repoName}",
            databasePath: $"/data/{repoName}.db")
        {
            Branch = "main"
        };
}
