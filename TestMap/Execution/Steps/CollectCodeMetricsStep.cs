using TestMap.App;
using TestMap.Services.StaticAnalysis;

namespace TestMap.Execution.Steps;

public class CollectCodeMetricsStep : IPipelineStep
{
    private readonly ICodeMetricsService _codeMetricsService;
    private readonly HashSet<string> _analyzedProjectIds = new();

    public CollectCodeMetricsStep(ICodeMetricsService codeMetricsService)
    {
        _codeMetricsService = codeMetricsService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        if (context == null)
        {
            return;
        }

        foreach (var solution in context.Project.Solutions)
        {
            if (!_analyzedProjectIds.Add(solution.FilePath))
            {
                context.Logger.Information($"Skipping already measured solution: {solution.FilePath}");
                continue;
            }

            await _codeMetricsService.CollectCodeMetricsAsync(solution);
        }
    }
}
