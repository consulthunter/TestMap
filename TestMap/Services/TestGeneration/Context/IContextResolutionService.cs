using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Context;

public interface IContextResolutionService
{
    IReadOnlyList<ContextResolutionResult> Resolve(ContextGraph graph);
}
