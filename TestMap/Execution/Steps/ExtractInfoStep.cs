using TestMap.App;
using TestMap.Services.ProjectDiscovery;
using TestMap.Models.Targets;
using TestMap.Services.TestGeneration.Workspace;

namespace TestMap.Execution.Steps;

public class ExtractInfoStep : IPipelineStep
{
    private readonly IExtractInformationService _extractInformationService;
    private readonly IWorkspaceIntegrityService _workspaceIntegrity;

    public ExtractInfoStep(
        IExtractInformationService extractInformationService,
        IWorkspaceIntegrityService workspaceIntegrity)
    {
        _extractInformationService = extractInformationService;
        _workspaceIntegrity = workspaceIntegrity;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        if (context?.MaterializedRevision is not null)
        {
            var observation = await _workspaceIntegrity.EvaluateAsync(IntegrityCheckpoint.PreExtraction);
            if (!observation.Status.IsVerified())
                throw new InvalidOperationException(
                    $"Pre-extraction workspace integrity failed with status '{observation.Status}'.");
        }
        await _extractInformationService.ExtractInfoAsync();
    }
}
