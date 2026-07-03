using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Context;

public interface IContextGraphService
{
    Task<ContextGraph> BuildAsync(
        TestGenerationRequest request,
        CancellationToken cancellationToken = default);
}
