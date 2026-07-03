using TestMap.Models.Results;
using TestMap.Models.Testing;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="TestRunMappingExtensions"/> covering <c>ToDomain</c> and
/// <c>ToEntity</c>. No database infrastructure required.
/// </summary>
public sealed class TestRunMappingExtensionsTests
{
    /// <summary>
    /// ToDomain maps the database primary key to DbId and the foreign key to DbProjectId,
    /// ensuring callers can correlate a returned domain object with the underlying row.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_MapsDbIdAndDbProjectId()
    {
        var entity = new TestRunEntity
        {
            Id = 17,
            ProjectId = 5,
            RunId = "baseline_001",
            RunDate = "2026-06-01",
            Success = true,
            Coverage = 80,
            LogPath = "/logs/run.log"
        };

        var model = entity.ToDomain();

        Assert.Equal(17, model.DbId);
        Assert.Equal(5, model.DbProjectId);
    }

    /// <summary>
    /// ToDomain maps all data fields from the entity to the domain model correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_MapsAllDataFields()
    {
        var entity = new TestRunEntity
        {
            Id = 1,
            ProjectId = 2,
            RunId = "run_abc",
            RunDate = "2026-05-01",
            Success = false,
            Coverage = 65,
            MutationScore = 0.42,
            LogPath = "/logs/abc.log",
            FailureAnalysis = new FailureAnalysisModel { Stage = "Build", Category = "CompilationError" }
        };

        var model = entity.ToDomain();

        Assert.Equal("run_abc", model.RunId);
        Assert.Equal("2026-05-01", model.RunDate);
        Assert.False(model.Success);
        Assert.Equal(65, model.Coverage);
        Assert.Equal(0.42, model.MutationScore);
        Assert.Equal("/logs/abc.log", model.LogPath);
        Assert.NotNull(model.FailureAnalysis);
        Assert.Equal("Build", model.FailureAnalysis!.Stage);
        Assert.Equal("CompilationError", model.FailureAnalysis.Category);
    }

    /// <summary>
    /// ToEntity(model, projectId) maps domain model fields to the entity and sets the
    /// supplied projectId on the entity's ProjectId column.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_WithProjectId_MapsAllFields()
    {
        var model = new TestRunModel
        {
            RunId = "baseline_002",
            RunDate = "2026-06-01",
            Success = true,
            Coverage = 90,
            MutationScore = 0.75,
            LogPath = "/logs/baseline.log"
        };

        var entity = model.ToEntity(projectId: 3);

        Assert.Equal(3, entity.ProjectId);
        Assert.Equal("baseline_002", entity.RunId);
        Assert.Equal("2026-06-01", entity.RunDate);
        Assert.True(entity.Success);
        Assert.Equal(90, entity.Coverage);
        Assert.Equal(0.75, entity.MutationScore);
        Assert.Equal("/logs/baseline.log", entity.LogPath);
        Assert.NotNull(entity.CreatedAt);
    }

    /// <summary>
    /// A round-trip through ToEntity then ToDomain preserves all data fields and
    /// reflects the correct database key back on the domain model.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_ToEntity_ToDomain_PreservesFields()
    {
        var original = new TestRunModel
        {
            RunId = "run_xyz",
            RunDate = "2026-04-15",
            Success = true,
            Coverage = 72,
            MutationScore = 0.5,
            LogPath = "/logs/xyz.log"
        };

        var entity = original.ToEntity(projectId: 7);
        entity.Id = 99; // simulate what EF sets after insert
        var restored = entity.ToDomain();

        Assert.Equal(99, restored.DbId);
        Assert.Equal(7, restored.DbProjectId);
        Assert.Equal(original.RunId, restored.RunId);
        Assert.Equal(original.RunDate, restored.RunDate);
        Assert.Equal(original.Success, restored.Success);
        Assert.Equal(original.Coverage, restored.Coverage);
        Assert.Equal(original.MutationScore, restored.MutationScore);
        Assert.Equal(original.LogPath, restored.LogPath);
    }
}
