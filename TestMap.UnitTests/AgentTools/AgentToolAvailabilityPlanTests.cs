using TestMap.Models.Configuration.Experiment;
using TestMap.Services.AgentTools;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.AgentTools;

public sealed class AgentToolAvailabilityPlanTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_AvailableTool_IsExecutable()
    {
        var tool = new ExperimentToolConfig { Id = "codex" };

        var plan = AgentToolAvailabilityPlan.Create(
            [tool],
            [new ToolAvailabilityResult { ToolId = "codex", IsAvailable = true }],
            requireAvailabilityInSetup: true);

        Assert.Single(plan.ExecutableTools);
        Assert.Empty(plan.SkippedTools);
        Assert.Empty(plan.SetupFailures);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_UnavailableRequiredTool_FailsSetup()
    {
        var tool = new ExperimentToolConfig { Id = "codex" };

        var plan = AgentToolAvailabilityPlan.Create(
            [tool],
            [new ToolAvailabilityResult { ToolId = "codex", IsAvailable = false, UnavailableReason = "image missing" }],
            requireAvailabilityInSetup: true);

        Assert.Empty(plan.ExecutableTools);
        var failure = Assert.Single(plan.SetupFailures);
        Assert.Equal("codex", failure.Tool.Id);
        Assert.Contains("image missing", failure.Reason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_UnavailableOptionalTool_IsSkipped()
    {
        var tool = new ExperimentToolConfig { Id = "codex" };

        var plan = AgentToolAvailabilityPlan.Create(
            [tool],
            [new ToolAvailabilityResult { ToolId = "codex", IsAvailable = false, UnavailableReason = "missing secret" }],
            requireAvailabilityInSetup: false);

        Assert.Empty(plan.ExecutableTools);
        var skipped = Assert.Single(plan.SkippedTools);
        Assert.Equal("codex", skipped.Tool.Id);
        Assert.Contains("missing secret", skipped.Reason);
        Assert.Empty(plan.SetupFailures);
    }
}
