using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public interface ITestBootstrapDetectionService
{
    Task<TestBootstrapDetectionResult> DetectAsync(CancellationToken cancellationToken = default);
}