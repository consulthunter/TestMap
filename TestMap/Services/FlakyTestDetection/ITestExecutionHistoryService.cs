using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public interface ITestExecutionHistoryService
{
    Task<IReadOnlyList<TestExecutionResultModel>> GetHistoryAsync(
        TestExecutionResultModel testIdentity,
        int historyWindowRuns,
        CancellationToken cancellationToken = default);
}