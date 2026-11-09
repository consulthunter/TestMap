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
    private readonly IConfigurationService _configurationService;

    public RunExperimentStep(
        IExperimentOrchestrationService orchestrationService,
        IConfigurationService configurationService)
    {
        _orchestrationService = orchestrationService;
        _configurationService = configurationService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        var experimentConfig = _configurationService.Config.ExperimentConfig;

        ValidateExperimentConfiguration(experimentConfig);

        await _orchestrationService.RunExperimentAsync(experimentConfig);
    }

    private void ValidateExperimentConfiguration(ExperimentConfig experimentConfig)
    {
        ExperimentConfigurationValidator.ValidateMatrixSettings(experimentConfig);

        if (experimentConfig.CandidateLimit <= 0)
            throw new InvalidOperationException("ExperimentConfig.CandidateLimit must be greater than 0.");

        var usableProviders = _configurationService.Config.AiProviderConfig.ProviderConfigs
            .Where(AiProviderConfigurationRules.IsUsable)
            .Select(x => x.Provider)
            .ToHashSet();

        if (experimentConfig.LlmArms.Count > 0)
            ValidateLlmArmProviders(experimentConfig);
        else if (experimentConfig.IncludeProviders.Count > 0)
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
            var resultsFilePath = ExperimentResultsWriter.ResolveResultsFilePath(experimentConfig);
            var outputDir = Path.GetDirectoryName(resultsFilePath);
            if (!string.IsNullOrWhiteSpace(outputDir)) Directory.CreateDirectory(outputDir);
        }
    }

    /// <summary>
    /// Validates the provider behind each declared arm. An arm that names its own model does not
    /// need one on the provider section, so the model requirement is waived for those — otherwise
    /// running three models through one provider would still force a placeholder model on it.
    /// </summary>
    private void ValidateLlmArmProviders(ExperimentConfig experimentConfig)
    {
        var defaultProvider = _configurationService.Config.TestingConfig.GenerationConfig.Provider;

        foreach (var arm in experimentConfig.LlmArms)
        {
            var provider = arm.Provider ?? defaultProvider;
            var providerConfig = _configurationService.Config.AiProviderConfig.GetProviderConfig(provider);
            if (providerConfig == null)
                throw new InvalidOperationException(
                    $"LLM arm '{arm.Id}' names provider '{provider}', which has no config section.");

            var suppliesModel = !string.IsNullOrWhiteSpace(arm.Model);
            var error = AiProviderConfigurationRules.GetValidationError(providerConfig, !suppliesModel);
            if (error != null)
                throw new InvalidOperationException(
                    $"LLM arm '{arm.Id}' is not configured for use. {error}");

            if (!suppliesModel && string.IsNullOrWhiteSpace(providerConfig.Model))
                throw new InvalidOperationException(
                    $"LLM arm '{arm.Id}' sets no Model and provider '{provider}' has none configured.");
        }
    }
}
