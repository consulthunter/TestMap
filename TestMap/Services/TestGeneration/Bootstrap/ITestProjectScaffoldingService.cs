using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public interface ITestProjectScaffoldingService
{
    Task<TestProjectScaffoldingResult> ScaffoldAsync(
        TestBootstrapRequest request,
        bool applyChanges = false,
        CancellationToken cancellationToken = default);
}