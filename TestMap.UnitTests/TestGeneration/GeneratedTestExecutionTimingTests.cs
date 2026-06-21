using TestMap.Models.Testing;
using TestMap.Services.TestGeneration.Execution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GeneratedTestExecutionTimingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveGeneratedTestExecutionTimeMs_SumsMatchingParameterizedCases()
    {
        IReadOnlyList<TestResultModel> results =
        [
            new()
            {
                TestName = "DemoTests.GeneratedTest(x: 1)",
                Duration = TimeSpan.FromMilliseconds(3.5)
            },
            new()
            {
                TestName = "DemoTests.GeneratedTest(x: 2)",
                Duration = TimeSpan.FromMilliseconds(4.25)
            },
            new()
            {
                TestName = "DemoTests.UnrelatedTest",
                Duration = TimeSpan.FromMilliseconds(100)
            }
        ];

        var duration = GeneratedTestExecutionService.ResolveGeneratedTestExecutionTimeMs(
            results,
            "GeneratedTest");

        Assert.Equal(7.75, duration);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveGeneratedTestExecutionTimeMs_NoMatchingResult_ReturnsNull()
    {
        IReadOnlyList<TestResultModel> results =
        [
            new()
            {
                TestName = "DemoTests.UnrelatedTest",
                Duration = TimeSpan.FromMilliseconds(5)
            }
        ];

        var duration = GeneratedTestExecutionService.ResolveGeneratedTestExecutionTimeMs(
            results,
            "GeneratedTest");

        Assert.Null(duration);
    }
}
