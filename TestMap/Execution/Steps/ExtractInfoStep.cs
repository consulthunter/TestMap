using TestMap.App;
using TestMap.Services.ProjectDiscovery;

namespace TestMap.Execution.Steps;

public class ExtractInfoStep : IPipelineStep
{
    private readonly IExtractInformationService _extractInformationService;

    public ExtractInfoStep(IExtractInformationService extractInformationService)
    {
        _extractInformationService = extractInformationService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _extractInformationService.ExtractInfoAsync();
    }
}