using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GenerationObjectivePolicyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveExecutor_TestSuiteExpansion_UsesBasicExtension()
    {
        var executor = GenerationObjectivePolicy.ResolveExecutor(TestGenerationObjective.TestSuiteExpansion);

        Assert.Equal(TestActionExecutorMode.BasicExtension, executor);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetAllowedActions_TestSuiteExpansion_AllowsOnlyNewOrExtensionActions()
    {
        var actions = GenerationObjectivePolicy.GetAllowedActions(TestGenerationObjective.TestSuiteExpansion);

        Assert.Contains(CandidateActionKind.GenerateNewTest, actions);
        Assert.Contains(CandidateActionKind.ExtendExistingTestSuite, actions);
        Assert.DoesNotContain(CandidateActionKind.ImproveExistingTest, actions);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(TestGenerationApproach.Naive, true)]
    [InlineData(TestGenerationApproach.MetricsDriven, true)]
    [InlineData(TestGenerationApproach.ActionAware, false)]
    public void IsApproachSupported_TestSuiteExpansion_ReservesActionAwareForFutureObjective(
        TestGenerationApproach approach,
        bool expected)
    {
        var supported = GenerationObjectivePolicy.IsApproachSupported(
            TestGenerationObjective.TestSuiteExpansion,
            approach);

        Assert.Equal(expected, supported);
    }
}
