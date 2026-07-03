using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public interface ITestTypeClassificationService
{
    Task<TestTypeClassificationResult> ClassifyAsync(
        string sourceMemberName,
        string sourceCode,
        CancellationToken cancellationToken = default);
}