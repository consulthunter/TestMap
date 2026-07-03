using TestMap.App;

namespace TestMap.Execution;

public class RunPipeline
{
    private readonly IEnumerable<IPipelineStep> _steps;

    public RunPipeline(IEnumerable<IPipelineStep> steps)
    {
        _steps = steps;
    }

    public async Task RunAsync(ProjectContext context)
    {
        foreach (var step in _steps) await step.ExecuteAsync(context);
    }
}