using TestMap.Models.Code;

namespace TestMap.Services.StaticAnalysis;

public interface ICodeMetricsService
{
    Task CollectCodeMetricsAsync(CSharpSolutionModel analysisSolution, CancellationToken cancellationToken = default);
    Task CollectCodeMetricsAsync(CSharpProjectModel analysisProject, CancellationToken cancellationToken = default);
}