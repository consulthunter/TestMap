using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Services.StaticAnalysis;

public interface IRoslynSourceTestTraceService
{
    Task<List<SourceTestMappingEntity>?> TraceAsync(
        int projectId,
        int solutionId,
        CancellationToken cancellationToken = default);
}
