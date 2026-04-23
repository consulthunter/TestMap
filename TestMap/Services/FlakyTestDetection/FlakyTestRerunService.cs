using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class FlakyTestRerunService : IFlakyTestRerunService
{
    public Task<IReadOnlyList<FlakyTestRerunResultModel>> RerunFailedTestsAsync(
        string runId,
        IReadOnlyList<TestExecutionResultModel> failedTests,
        FlakyTestDetectionConfig config,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<FlakyTestRerunResultModel>>([]);
    }
}
