using TestMap.App;
using TestMap.Services.StaticAnalysis;

namespace TestMap.Execution.Steps;

public class EnrichTestMetadataStep : IPipelineStep
{
    private readonly ITestMetadataEnrichmentService _testMetadataEnrichmentService;

    public EnrichTestMetadataStep(ITestMetadataEnrichmentService testMetadataEnrichmentService)
    {
        _testMetadataEnrichmentService = testMetadataEnrichmentService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _testMetadataEnrichmentService.EnrichAsync();
    }
}
