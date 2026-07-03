using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="CandidateInventoryMappingExtensions"/>. Covers round-trip fidelity,
/// enum parsing, and the CreatedAt preservation fix on <c>ToEntity</c>.
/// </summary>
public sealed class CandidateInventoryMappingExtensionsTests
{
    /// <summary>
    /// ToDomain maps all scalar fields from the entity to the domain model, including
    /// the CreatedAt timestamp that was previously silently dropped.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_MapsAllFields()
    {
        var createdAt = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var entity = new CandidateInventoryEntity
        {
            Id = 7,
            ProjectId = 2,
            SourceMemberId = 100,
            SourceMethodName = "Calculate",
            SourceMethodSignature = "public int Calculate(int x)",
            SelectionStrategy = "Existing",
            IsExperimentEligible = true,
            InitialCoverage = 0.55,
            ComplexityScore = 3.0,
            TestState = "NeedsTestImprovement",
            RecommendedAction = "GenerateNewTest",
            SelectionTime = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = createdAt,
            TraceJson = "{}",
            AccessPathMemberIdsJson = "[]",
            CandidateMetadataJson = "{}"
        };

        var item = entity.ToDomain();

        Assert.Equal(7, item.Id);
        Assert.Equal(2, item.ProjectId);
        Assert.Equal(100, item.SourceMemberId);
        Assert.Equal("Calculate", item.SourceMethodName);
        Assert.Equal(TargetSelectionStrategy.Existing, item.SelectionStrategy);
        Assert.True(item.IsExperimentEligible);
        Assert.Equal(0.55, item.InitialCoverage);
        Assert.Equal(3.0, item.ComplexityScore);
        Assert.Equal(CandidateTestState.NeedsTestImprovement, item.TestState);
        Assert.Equal(CandidateActionKind.GenerateNewTest, item.RecommendedAction);
        Assert.Equal(createdAt, item.CreatedAt);
    }

    /// <summary>
    /// ToEntity preserves a non-default CreatedAt from the domain model rather than
    /// overwriting it with DateTime.UtcNow on every save.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_PreservesCreatedAtWhenAlreadySet()
    {
        var originalCreatedAt = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc);
        var item = MakeItem();
        item.CreatedAt = originalCreatedAt;

        var entity = item.ToEntity();

        Assert.Equal(originalCreatedAt, entity.CreatedAt);
    }

    /// <summary>
    /// ToEntity defaults CreatedAt to a recent UtcNow when the domain model has the
    /// zero-value default, i.e. the item was never read from the database.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_DefaultsCreatedAtToUtcNowWhenNotSet()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var item = MakeItem(); // CreatedAt stays default(DateTime)

        var entity = item.ToEntity();

        Assert.True(entity.CreatedAt >= before);
        Assert.True(entity.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    /// <summary>
    /// SelectionStrategy, TestState, and RecommendedAction are stored as strings and parse
    /// back to the original enum values when the entity is converted to a domain object.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_EnumFieldsRoundtripThroughStringStorage()
    {
        var item = MakeItem();
        item.SelectionStrategy = TargetSelectionStrategy.MetricDrivenImprovement;
        item.TestState = CandidateTestState.NeedsTestImprovement;
        item.RecommendedAction = CandidateActionKind.ImproveExistingTest;

        var entity = item.ToEntity();
        var restored = entity.ToDomain();

        Assert.Equal(TargetSelectionStrategy.MetricDrivenImprovement, restored.SelectionStrategy);
        Assert.Equal(CandidateTestState.NeedsTestImprovement, restored.TestState);
        Assert.Equal(CandidateActionKind.ImproveExistingTest, restored.RecommendedAction);
    }

    /// <summary>
    /// A full round-trip (domain → entity → domain) preserves all fields including the
    /// CreatedAt timestamp, enum values, and scalar numeric fields.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_ToEntity_ToDomain_PreservesAllFields()
    {
        var createdAt = new DateTime(2026, 2, 20, 12, 0, 0, DateTimeKind.Utc);
        var original = new CandidateInventoryItem
        {
            Id = 1,
            ProjectId = 1,
            SourceMemberId = 10,
            SourceMethodName = "Process",
            SourceMethodSignature = "public void Process()",
            IsExperimentEligible = true,
            InitialCoverage = 0.33,
            ComplexityScore = 5.5,
            SelectionStrategy = TargetSelectionStrategy.RiskWeighted,
            TestState = CandidateTestState.NoKnownTest,
            RecommendedAction = CandidateActionKind.GenerateNewTest,
            CreatedAt = createdAt,
            TraceJson = "{}",
            AccessPathMemberIdsJson = "[]",
            CandidateMetadataJson = "{}"
        };

        var restored = original.ToEntity().ToDomain();

        Assert.Equal(original.SourceMethodName, restored.SourceMethodName);
        Assert.Equal(original.IsExperimentEligible, restored.IsExperimentEligible);
        Assert.Equal(original.InitialCoverage, restored.InitialCoverage);
        Assert.Equal(original.ComplexityScore, restored.ComplexityScore);
        Assert.Equal(original.SelectionStrategy, restored.SelectionStrategy);
        Assert.Equal(original.TestState, restored.TestState);
        Assert.Equal(original.RecommendedAction, restored.RecommendedAction);
        Assert.Equal(createdAt, restored.CreatedAt);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static CandidateInventoryItem MakeItem() => new()
    {
        Id = 1,
        ProjectId = 1,
        SourceMemberId = 10,
        SourceMethodName = "Method",
        SourceMethodSignature = "public void Method()",
        SelectionStrategy = TargetSelectionStrategy.Existing,
        IsExperimentEligible = true,
        TraceJson = "{}",
        AccessPathMemberIdsJson = "[]",
        CandidateMetadataJson = "{}"
    };
}
