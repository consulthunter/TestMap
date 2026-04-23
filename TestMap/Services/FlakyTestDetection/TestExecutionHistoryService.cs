using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef.Repositories.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class TestExecutionHistoryService : ITestExecutionHistoryService
{
    private readonly TestExecutionResultRepository _repository;

    public TestExecutionHistoryService(TestExecutionResultRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<TestExecutionResultModel>> GetHistoryAsync(
        TestExecutionResultModel testIdentity,
        int historyWindowRuns,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetHistoryAsync(testIdentity, historyWindowRuns, cancellationToken);
    }
}
