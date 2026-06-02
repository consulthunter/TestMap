using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ExperimentOrchestrationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ShouldRequirePassingExistingTest_ForTestSuiteExpansion_ReturnsFalse()
    {
        var result = ExperimentOrchestrationService.ShouldRequirePassingExistingTest(
            TestGenerationObjective.TestSuiteExpansion);

        Assert.False(result);
    }
}
