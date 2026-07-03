using TestMap.Models;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Mapping;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="ProjectMappingExtensions"/> covering both <c>ToDomain</c> and
/// <c>ToEntity</c>. Each test uses simple value objects with no infrastructure dependencies.
/// </summary>
public sealed class ProjectMappingExtensionsTests
{
    /// <summary>
    /// ToDomain maps all entity fields to the corresponding domain model properties,
    /// including the database PK (DbId), owner, repo name, paths, branch, and commit.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_MapsAllFieldsFromEntity()
    {
        var entity = new ProjectEntity
        {
            Id = 42,
            Owner = "acme",
            RepoName = "widget",
            WebUrl = "https://github.com/acme/widget",
            DirectoryPath = "/repos/widget",
            DatabasePath = "/data/widget.db",
            Branch = "main",
            LastAnalyzedCommit = "abc123",
            ContentHash = "deadbeef",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        var model = entity.ToDomain();

        Assert.Equal(42, model.DbId);
        Assert.Equal("acme", model.Owner);
        Assert.Equal("widget", model.RepoName);
        Assert.Equal("https://github.com/acme/widget", model.GitHubUrl);
        Assert.Equal("/repos/widget", model.DirectoryPath);
        Assert.Equal("/data/widget.db", model.DatabasePath);
        Assert.Equal("main", model.Branch);
        Assert.Equal("abc123", model.LastAnalyzedCommit);
    }

    /// <summary>
    /// ToDomain treats a null WebUrl as an empty string rather than propagating null,
    /// and nullable path/branch/commit fields map cleanly when the entity columns are null.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_NullOptionalFieldsMapToDefaultsOrNull()
    {
        var entity = new ProjectEntity
        {
            Id = 1,
            Owner = "owner",
            RepoName = "repo",
            WebUrl = null,
            DatabasePath = null,
            Branch = null,
            LastAnalyzedCommit = null
        };

        var model = entity.ToDomain();

        Assert.Equal(string.Empty, model.GitHubUrl);
        Assert.Null(model.DatabasePath);
        Assert.Null(model.Branch);
        Assert.Null(model.LastAnalyzedCommit);
    }

    /// <summary>
    /// ToEntity maps all domain model fields that have entity counterparts, including URL,
    /// owner, repo name, directory path, branch, last-analyzed commit, database path, and content hash.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_MapsAllFieldsFromModel()
    {
        var model = new ProjectModel(
            gitHubUrl: "https://github.com/foo/bar",
            owner: "foo",
            repoName: "bar",
            directoryPath: "/repos/bar",
            databasePath: "/data/bar.db")
        {
            Branch = "feature/x",
            LastAnalyzedCommit = "deadcafe"
        };

        var entity = model.ToEntity();

        Assert.Equal("foo", entity.Owner);
        Assert.Equal("bar", entity.RepoName);
        Assert.Equal("https://github.com/foo/bar", entity.WebUrl);
        Assert.Equal("/repos/bar", entity.DirectoryPath);
        Assert.Equal("/data/bar.db", entity.DatabasePath);
        Assert.Equal("feature/x", entity.Branch);
        Assert.Equal("deadcafe", entity.LastAnalyzedCommit);
        Assert.NotNull(entity.ContentHash);
    }

    /// <summary>
    /// A round-trip through ToEntity then ToDomain preserves the core identity fields:
    /// owner, repo name, URL, directory, database path, branch, and last-analyzed commit.
    /// DbId is not part of ToEntity so it cannot be verified in a round-trip without a real
    /// database insert.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_ToEntity_ToDomain_PreservesCoreFields()
    {
        var original = new ProjectModel(
            gitHubUrl: "https://github.com/org/svc",
            owner: "org",
            repoName: "svc",
            directoryPath: "/repos/svc",
            databasePath: "/data/svc.db")
        {
            Branch = "dev",
            LastAnalyzedCommit = "feedface"
        };

        var entity = original.ToEntity();
        var restored = entity.ToDomain();

        Assert.Equal(original.Owner, restored.Owner);
        Assert.Equal(original.RepoName, restored.RepoName);
        Assert.Equal(original.GitHubUrl, restored.GitHubUrl);
        Assert.Equal(original.DirectoryPath, restored.DirectoryPath);
        Assert.Equal(original.DatabasePath, restored.DatabasePath);
        Assert.Equal(original.Branch, restored.Branch);
        Assert.Equal(original.LastAnalyzedCommit, restored.LastAnalyzedCommit);
    }
}
