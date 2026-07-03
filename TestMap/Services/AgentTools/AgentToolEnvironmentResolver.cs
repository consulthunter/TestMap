using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Models.Configuration.AiProviders.Ollama;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Services.AgentTools;

/// <summary>
/// Resolves the normalized TESTMAP_LLM_* environment variables and secret checks for a tool
/// attempt. API key values are returned in NormalizedVars only — they are never persisted.
/// </summary>
public sealed class AgentToolEnvironmentResolver : IAgentToolEnvironmentResolver
{
    public ToolEnvironmentResolution Resolve(
        ExperimentToolConfig tool,
        AiProviderConfig providers,
        GenerationConfig effectiveGenerationConfig)
    {
        // 1. Determine effective provider.
        var effectiveProvider = tool.Provider ?? effectiveGenerationConfig.Provider;
        var providerConfig = providers.GetProviderConfig(effectiveProvider);

        // 2. Build normalized vars (in-memory only — includes API key).
        var normalized = new Dictionary<string, string>();
        var metadata = new Dictionary<string, string>();
        var warnings = new List<string>();

        var providerId = ToProviderId(effectiveProvider);
        normalized["TESTMAP_LLM_PROVIDER"] = providerId;
        metadata["provider_id"] = providerId;

        var model = tool.Model
            ?? (providerConfig != null ? providerConfig.Model : string.Empty);
        if (!string.IsNullOrEmpty(model))
        {
            normalized["TESTMAP_LLM_MODEL"] = model;
            metadata["model"] = model;
        }

        var apiKey = ResolveApiKey(effectiveProvider, providerConfig);
        if (!string.IsNullOrEmpty(apiKey))
        {
            // API key goes into NormalizedVars only — never into PersistableMetadata.
            normalized["TESTMAP_LLM_API_KEY"] = apiKey;
        }
        else if (providerConfig == null)
        {
            warnings.Add($"No provider config found for provider '{effectiveProvider}'.");
        }

        // 3. Emit base URL when applicable.
        var baseUrl = ResolveBaseUrl(tool, effectiveProvider, providerConfig);
        if (!string.IsNullOrEmpty(baseUrl))
        {
            normalized["TESTMAP_LLM_BASE_URL"] = baseUrl;
            metadata["base_url"] = baseUrl;
        }

        ApplyToolSpecificSecrets(tool, normalized);

        // 4. Check required env vars against host environment.
        var missing = new List<string>();
        foreach (var varName in tool.RequiredEnvironmentVariables)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(varName)))
                missing.Add(varName);
        }

        var canonicalSecretName = ResolveCanonicalSecretNames(effectiveProvider).FirstOrDefault() ?? string.Empty;
        if (RequiresNormalizedApiKey(tool.Id) &&
            string.IsNullOrEmpty(apiKey) &&
            !string.IsNullOrEmpty(canonicalSecretName) &&
            !AnyConfiguredEnvironmentVariable(ResolveCanonicalSecretNames(effectiveProvider)) &&
            !missing.Contains(canonicalSecretName, StringComparer.OrdinalIgnoreCase))
        {
            missing.Add(canonicalSecretName);
        }

        // 5. Copy non-secret tool environment vars.
        var toolVars = new Dictionary<string, string>(tool.Environment);

        return new ToolEnvironmentResolution
        {
            NormalizedVars = normalized,
            ToolVars = toolVars,
            PersistableMetadata = metadata,
            MissingRequiredSecrets = missing,
            Warnings = warnings
        };
    }

    private static string ToProviderId(AiProvider provider) => provider switch
    {
        AiProvider.OpenAi => "openai",
        AiProvider.CustomOpenAi => "openai",
        AiProvider.Anthropic => "anthropic",
        AiProvider.GoogleGemini => "google-gemini",
        AiProvider.GoogleCloud => "google-cloud",
        AiProvider.Ollama => "ollama",
        AiProvider.Amazon => "amazon",
        _ => provider.ToString().ToLowerInvariant()
    };

    private static string? ResolveBaseUrl(
        ExperimentToolConfig tool,
        AiProvider provider,
        IAiProviderConfig? config)
    {
        if (!string.IsNullOrWhiteSpace(tool.Endpoint))
            return tool.Endpoint;

        if (provider == AiProvider.CustomOpenAi && config is CustomOpenAiConfig customConfig)
            return customConfig.Endpoint;

        if (provider == AiProvider.Ollama && config is OllamaConfig ollamaConfig)
            return ollamaConfig.Endpoint;

        return null;
    }

    private static string ResolveApiKey(AiProvider provider, IAiProviderConfig? config)
    {
        if (!string.IsNullOrEmpty(config?.ApiKey))
            return config.ApiKey;

        foreach (var secretName in ResolveCanonicalSecretNames(provider))
        {
            var value = Environment.GetEnvironmentVariable(secretName);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveCanonicalSecretNames(AiProvider provider) => provider switch
    {
        AiProvider.OpenAi => ["OPENAI_API_KEY"],
        AiProvider.CustomOpenAi => ["CUSTOM_API_KEY"],
        AiProvider.Anthropic => ["ANTHROPIC_API_KEY", "ANTHROPIC_KEY"],
        AiProvider.GoogleGemini or AiProvider.GoogleCloud => ["GEMINI_API_KEY", "GOOGLE_GEMINI_API_KEY", "GOOGLE_API_KEY"],
        _ => []
    };

    private static bool AnyConfiguredEnvironmentVariable(IEnumerable<string> names)
    {
        return names.Any(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));
    }

    private static void ApplyToolSpecificSecrets(
        ExperimentToolConfig tool,
        IDictionary<string, string> normalized)
    {
        if (tool.Id.Equals("copilot", StringComparison.OrdinalIgnoreCase))
        {
            var token = Environment.GetEnvironmentVariable("GITHUB_COPILOT_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
                normalized["GITHUB_COPILOT_TOKEN"] = token;
        }
    }

    private static bool RequiresNormalizedApiKey(string toolId)
    {
        return toolId.Equals("codex", StringComparison.OrdinalIgnoreCase) ||
               toolId.Equals("claude", StringComparison.OrdinalIgnoreCase) ||
               toolId.Equals("aider", StringComparison.OrdinalIgnoreCase) ||
               toolId.Equals("openhands", StringComparison.OrdinalIgnoreCase) ||
               toolId.Equals("mini-swe-agent", StringComparison.OrdinalIgnoreCase) ||
               toolId.Equals("gemini", StringComparison.OrdinalIgnoreCase);
    }
}
