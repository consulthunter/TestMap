using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public interface IFlakyTestRerunService
{
    Task<IReadOnlyList<FlakyTestRerunResultModel>> RerunFailedTestsAsync(
        string runId,
        IReadOnlyList<TestExecutionResultModel> failedTests,
        FlakyTestDetectionConfig config,
        CancellationToken cancellationToken = default);
}