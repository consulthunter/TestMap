using TestMap.App;
using TestMap.Services.StaticAnalysis;

namespace TestMap.Execution.Steps;

public class CollectTestSmellsStep : IPipelineStep
{
    private readonly ITestSmellService _testSmellService;

    public CollectTestSmellsStep(ITestSmellService testSmellService)
    {
        _testSmellService = testSmellService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        if (context == null || context.Project.DbId == 0)
        {
            return;
        }

        foreach (var solution in context.Project.Solutions)
        {
            await _testSmellService.CollectAsync(solution.FilePath, context.Project.DbId);
        }
    }
}
