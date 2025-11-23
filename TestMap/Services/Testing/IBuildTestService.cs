using TestMap.Models.Results;

namespace TestMap.Services.Testing;

public interface IBuildTestService
{
    Task<TestRunResult> BuildTestAsync(List<string> solutions, bool isBaseline, string? methodName = null);
}