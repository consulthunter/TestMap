using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public sealed class TestTypeClassificationService : ITestTypeClassificationService
{
    public Task<TestTypeClassificationResult> ClassifyAsync(
        string sourceMemberName,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TestTypeClassificationResult
        {
            TestType = BootstrapTestType.Unit,
            Reason = "Bootstrap mode defaults to unit tests for the initial implementation."
        });
    }
}