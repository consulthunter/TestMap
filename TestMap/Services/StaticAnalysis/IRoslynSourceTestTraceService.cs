using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Services.StaticAnalysis;

public interface IRoslynSourceTestTraceService
{
    /// <summary>
    /// Traces the test methods in <paramref name="testMemberIds"/> to the production members
    /// they reach. Calls resolve only to members in <paramref name="indexMemberIds"/>: the
    /// versions an analysis pass observed in the current workspace.
    /// </summary>
    Task<List<SourceTestMappingEntity>?> TraceAsync(
        int projectId,
        int solutionId,
        IReadOnlySet<int> indexMemberIds,
        IReadOnlySet<int> testMemberIds,
        CancellationToken cancellationToken = default);
}
