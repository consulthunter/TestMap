using TestMap.App;
using TestMap.Services.TestGeneration;

namespace TestMap.Execution.Steps;

public class GenerateTestsStep : IPipelineStep
{
    private readonly IGenerateTestService _generateTestService;

    public GenerateTestsStep(IGenerateTestService generateTestService)
    {
        _generateTestService = generateTestService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _generateTestService.GenerateTestAsync();
    }
}