using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GenerationBudgetPlannerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Plan_PassAt1_UsesSingleGenerationAttempt()
    {
        var attempts = GenerationBudgetPlanner.Plan(GenerationBudgetMode.PassAt1);

        var attempt = Assert.Single(attempts);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.False(attempt.IsRepair);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Plan_PassAt5_UsesFiveFreshGenerationAttempts()
    {
        var attempts = GenerationBudgetPlanner.Plan(GenerationBudgetMode.PassAt5);

        Assert.Equal([1, 2, 3, 4, 5], attempts.Select(x => x.AttemptNumber));
        Assert.All(attempts, x => Assert.False(x.IsRepair));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Plan_RepairAt5_UsesOneGenerationAttemptThenFourRepairs()
    {
        var attempts = GenerationBudgetPlanner.Plan(GenerationBudgetMode.PassAt1RepairAt5);

        Assert.Equal([1, 2, 3, 4, 5], attempts.Select(x => x.AttemptNumber));
        Assert.False(attempts[0].IsRepair);
        Assert.All(attempts.Skip(1), x => Assert.True(x.IsRepair));
    }
}
