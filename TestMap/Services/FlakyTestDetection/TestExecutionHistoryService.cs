using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef.Repositories.Testing;

namespace TestMap.Services.FlakyTestDetection;

public class TestExecutionHistoryService : ITestExecutionHistoryService
{
    private readonly TestResultRepository _repository;

    public TestExecutionHistoryService(TestResultRepository repository)
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
