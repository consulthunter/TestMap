using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Services.Configuration;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.Experiment.Reporting;

namespace TestMap.Execution.Steps;

/// <summary>
/// Pipeline step for running AI provider comparison experiments.
/// </summary>
public class RunExperimentStep : IPipelineStep
{
    private readonly IExperimentOrchestrationService _orchestrationService;
    private readonly IExperimentAnalysisService _analysisService;
    private readonly IConfigurationService _configurationService;

    public RunExperimentStep(
        IExperimentOrchestrationService orchestrationService,
        IExperimentAnalysisService analysisService,
        IConfigurationService configurationService)
    {
        _orchestrationService = orchestrationService;
        _analysisService = analysisService;
        _configurationService = configurationService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        var experimentConfig = _configurationService.Config.ExperimentConfig;

        ValidateExperimentConfiguration(experimentConfig);

        var experimentRun = await _orchestrationService.RunExperimentAsync(experimentConfig);

        if (!string.IsNullOrWhiteSpace(experimentConfig.OutputPath))
        {
            var outputDir = Path.GetDirectoryName(experimentConfig.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDir)) Directory.CreateDirectory(outputDir);

            await _analysisService.ExportToCsvAsync(experimentRun.Id, experimentConfig.OutputPath);
        }
    }

    private void ValidateExperimentConfiguration(ExperimentConfig experimentConfig)
    {
        if (experimentConfig.CandidateLimit <= 0)
            throw new InvalidOperationException("ExperimentConfig.CandidateLimit must be greater than 0.");

        if (experimentConfig.Strategies == null || experimentConfig.Strategies.Count == 0)
            throw new InvalidOperationException("ExperimentConfig.Strategies must contain at least one strategy.");

        var usableProviders = _configurationService.Config.AiProviderConfig.ProviderConfigs
            .Where(AiProviderConfigurationRules.IsUsable)
            .Select(x => x.Provider)
            .ToHashSet();

        if (experimentConfig.IncludeProviders.Count > 0)
            foreach (var providerName in experimentConfig.IncludeProviders)
            {
                if (!Enum.TryParse<AiProvider>(providerName, true, out var provider))
                    throw new InvalidOperationException($"Unknown experiment provider '{providerName}'.");

                if (!usableProviders.Contains(provider))
                {
                    var providerConfig = _configurationService.Config.AiProviderConfig.GetProviderConfig(provider);
                    var detail = providerConfig == null
                        ? "Provider config section is missing."
                        : AiProviderConfigurationRules.GetValidationError(providerConfig) ??
                          "Provider config is invalid.";
                    throw new InvalidOperationException(
                        $"Experiment provider '{provider}' is not configured for use. {detail}");
                }
            }
        else if (usableProviders.Count == 0)
            throw new InvalidOperationException(
                "ExperimentConfig requires at least one usable provider in AiProviderConfig.ProviderConfigs.");

        if (!string.IsNullOrWhiteSpace(experimentConfig.PreferredProvider))
        {
            if (!Enum.TryParse<AiProvider>(experimentConfig.PreferredProvider, true, out var preferredProvider))
                throw new InvalidOperationException(
                    $"Unknown preferred provider '{experimentConfig.PreferredProvider}'.");

            if (!usableProviders.Contains(preferredProvider))
            {
                var providerConfig = _configurationService.Config.AiProviderConfig.GetProviderConfig(preferredProvider);
                var detail = providerConfig == null
                    ? "Provider config section is missing."
                    : AiProviderConfigurationRules.GetValidationError(providerConfig) ?? "Provider config is invalid.";
                throw new InvalidOperationException(
                    $"Preferred provider '{preferredProvider}' is not configured for use. {detail}");
            }
        }

        if (!string.IsNullOrWhiteSpace(experimentConfig.OutputPath))
        {
            var outputDir = Path.GetDirectoryName(experimentConfig.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDir)) Directory.CreateDirectory(outputDir);
        }
    }
}
