using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="GenerationAttemptMappingExtensions"/>. Covers BudgetMode
/// resolution and the removal of the legacy Strategy-column fallback.
/// </summary>
public sealed class GenerationAttemptMappingExtensionsTests
{
    /// <summary>
    /// When the BudgetMode column holds a valid enum name, ToDomain parses it directly
    /// and returns the correct GenerationBudgetMode value.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("PassAt1", GenerationBudgetMode.PassAt1)]
    [InlineData("PassAt5", GenerationBudgetMode.PassAt5)]
    [InlineData("PassAt1RepairAt5", GenerationBudgetMode.PassAt1RepairAt5)]
    [InlineData("passat1", GenerationBudgetMode.PassAt1)] // case-insensitive
    public void ToDomain_ParsesBudgetModeFieldCorrectly(string budgetModeValue, GenerationBudgetMode expected)
    {
        var entity = MakeEntity(budgetMode: budgetModeValue);

        var attempt = entity.ToDomain();

        Assert.Equal(expected, attempt.BudgetMode);
    }

    /// <summary>
    /// When BudgetMode is empty or an unrecognised string, ToDomain falls back to
    /// PassAt1 rather than throwing. The legacy Strategy column is not consulted.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("")]
    [InlineData("Pass1")]        // old alias — no longer recognised
    [InlineData("Pass5")]        // old alias — no longer recognised
    [InlineData("Repair5")]      // old alias — no longer recognised
    [InlineData("garbage")]
    public void ToDomain_FallsBackToPassAt1ForUnknownBudgetMode(string budgetModeValue)
    {
        // Strategy is set to a parseable value to confirm the shim is gone — BudgetMode wins.
        var entity = MakeEntity(budgetMode: budgetModeValue, strategy: "PassAt5");

        var attempt = entity.ToDomain();

        Assert.Equal(GenerationBudgetMode.PassAt1, attempt.BudgetMode);
    }

    /// <summary>
    /// ToEntity writes the BudgetMode enum name to the BudgetMode column.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_WritesBudgetModeColumnFromDomainValue()
    {
        var attempt = MakeAttempt(GenerationBudgetMode.PassAt1RepairAt5);

        var entity = attempt.ToEntity();

        Assert.Equal("PassAt1RepairAt5", entity.BudgetMode);
    }

    /// <summary>
    /// A round-trip through ToEntity then ToDomain preserves the BudgetMode value.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(GenerationBudgetMode.PassAt1)]
    [InlineData(GenerationBudgetMode.PassAt5)]
    [InlineData(GenerationBudgetMode.PassAt1RepairAt5)]
    public void RoundTrip_BudgetMode_IsPreserved(GenerationBudgetMode mode)
    {
        var attempt = MakeAttempt(mode);

        var restored = attempt.ToEntity().ToDomain();

        Assert.Equal(mode, restored.BudgetMode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_TimingFields_ArePreserved()
    {
        var attempt = MakeAttempt();
        attempt.GenerationDurationSeconds = 12.5;
        attempt.ValidationDurationSeconds = 3.25;
        attempt.TotalDurationSeconds = 15.75;

        var restored = attempt.ToEntity().ToDomain();

        Assert.Equal(12.5, restored.GenerationDurationSeconds);
        Assert.Equal(3.25, restored.ValidationDurationSeconds);
        Assert.Equal(15.75, restored.TotalDurationSeconds);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_ModifiedFileSnapshot_IsPreserved()
    {
        var attempt = MakeAttempt();
        attempt.ModifiedFilePath = @"D:\repo\Tests\WidgetTests.cs";
        attempt.ModifiedFileContents = "public class WidgetTests { }";
        attempt.ModifiedFileSha256 = new string('a', 64);

        var restored = attempt.ToEntity().ToDomain();

        Assert.Equal(attempt.ModifiedFilePath, restored.ModifiedFilePath);
        Assert.Equal(attempt.ModifiedFileContents, restored.ModifiedFileContents);
        Assert.Equal(attempt.ModifiedFileSha256, restored.ModifiedFileSha256);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static GenerationAttemptEntity MakeEntity(
        string budgetMode = "PassAt1",
        string strategy = "") => new()
    {
        Id = 1,
        CandidateMethodId = 10,
        ProviderName = "OpenAi",
        ModelName = "gpt-4o",
        Strategy = strategy,
        BudgetMode = budgetMode,
        Objective = "TestSuiteExpansion",
        GenerationApproach = "MetricsDriven",
        ContextMode = "ChainedHistory",
        StartTime = DateTime.UtcNow,
        Status = "Completed",
        FailureKind = "None",
        FailureStage = string.Empty,
        FailureCategory = string.Empty,
        ErrorMessage = string.Empty,
        StepConfigJson = string.Empty,
        EffectiveProfileJson = string.Empty,
        EffectiveProfileHash = string.Empty,
        RuleDecisionSnapshotJson = string.Empty,
        MetricsPath = string.Empty,
        AblationVariantId = string.Empty
    };

    private static GenerationAttempt MakeAttempt(
        GenerationBudgetMode budgetMode = GenerationBudgetMode.PassAt1) => new()
    {
        Id = 1,
        CandidateMethodId = 10,
        Provider = AiProvider.OpenAi,
        ModelName = "gpt-4o",
        BudgetMode = budgetMode,
        Objective = TestGenerationObjective.TestSuiteExpansion,
        GenerationApproach = TestGenerationApproach.MetricsDriven,
        ContextMode = GenerationContextMode.ChainedHistory,
        StartedAt = DateTime.UtcNow,
        AttemptNumber = 1
    };
}
