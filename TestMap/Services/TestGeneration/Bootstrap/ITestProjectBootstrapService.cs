using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public interface ITestProjectBootstrapService
{
    Task<TestProjectBootstrapResult> CreateTestProjectAsync(
        TestBootstrapRequest request,
        bool applyChanges = false,
        CancellationToken cancellationToken = default);
}