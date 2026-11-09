using TestMap.Models.AgentTools;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Models.Configuration.AiProviders.OpenAI;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.AgentTools;
using TestMap.Services.Experiment.Evaluation.AgentTools;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Regression tests for provider-suffixed tool entries. An experiment runs the same tool against
/// several providers (copilot-openai, copilot-custom, ...), which forces distinct
/// <see cref="ExperimentToolConfig.Id"/> values. Everything that asks "which tool is this?" must
/// branch on <see cref="ExperimentToolConfig.Family"/> instead, or those entries silently lose
/// their secrets, provider env vars and artifact parsing.
/// </summary>
public sealed class AgentToolFamilyResolutionTests
{
    private static AiProviderConfig MakeProviders() =>
        new()
        {
            OpenAi = new OpenAiConfig { ApiKey = "sk-test", Model = "gpt-4o" },
            CustomOpenAi = new CustomOpenAiConfig
            {
                ApiKey = "custom-key",
                Model = "GLM-5.3",
                Endpoint = "https://llm-api.example.test/api/"
            }
        };

    private static GenerationConfig MakeGenerationConfig(AiProvider provider = AiProvider.OpenAi) =>
        new() { Provider = provider };

    /// <summary>
    /// Family falls back to Id when ImageKey is absent, so bare entries keep working unchanged.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("copilot", null, "copilot")]
    [InlineData("copilot-custom", "copilot", "copilot")]
    [InlineData("mini-swe-agent-openai", "mini-swe-agent", "mini-swe-agent")]
    [InlineData("aider", "", "aider")]
    public void Family_ResolvesImageKeyThenId(string id, string? imageKey, string expected)
    {
        var tool = new ExperimentToolConfig { Id = id, ImageKey = imageKey };

        Assert.Equal(expected, tool.Family);
    }

    /// <summary>
    /// The Copilot CLI authenticates with GITHUB_COPILOT_TOKEN. A provider-suffixed entry must
    /// still get it forwarded, otherwise the container dies with "No authentication information
    /// found" before doing any work.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ProviderSuffixedCopilot_ForwardsGithubToken()
    {
        var resolver = new AgentToolEnvironmentResolver();
        var previous = Environment.GetEnvironmentVariable("GITHUB_COPILOT_TOKEN");
        Environment.SetEnvironmentVariable("GITHUB_COPILOT_TOKEN", "copilot-token");
        try
        {
            var tool = new ExperimentToolConfig
            {
                Id = "copilot-custom",
                ImageKey = "copilot",
                Provider = AiProvider.CustomOpenAi,
                RequiredEnvironmentVariables = ["GITHUB_COPILOT_TOKEN"]
            };

            var result = resolver.Resolve(tool, MakeProviders(), MakeGenerationConfig(AiProvider.CustomOpenAi));

            Assert.Equal("copilot-token", result.NormalizedVars["GITHUB_COPILOT_TOKEN"]);
            Assert.DoesNotContain(result.PersistableMetadata, kvp => kvp.Value == "copilot-token");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_COPILOT_TOKEN", previous);
        }
    }

    /// <summary>
    /// A suffixed BYOK entry missing its provider key still fails at availability time rather
    /// than 401-ing partway through the attempt.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ProviderSuffixedCopilotByokWithoutKey_ReportsMissingSecret()
    {
        var resolver = new AgentToolEnvironmentResolver();
        var previousCustom = Environment.GetEnvironmentVariable("CUSTOM_API_KEY");
        Environment.SetEnvironmentVariable("CUSTOM_API_KEY", null);
        try
        {
            var providers = new AiProviderConfig
            {
                CustomOpenAi = new CustomOpenAiConfig
                {
                    ApiKey = string.Empty,
                    Model = "GLM-5.3",
                    Endpoint = "https://llm-api.example.test/api/"
                }
            };

            var result = resolver.Resolve(
                new ExperimentToolConfig
                {
                    Id = "copilot-custom",
                    ImageKey = "copilot",
                    Provider = AiProvider.CustomOpenAi
                },
                providers,
                MakeGenerationConfig(AiProvider.CustomOpenAi));

            Assert.False(result.IsValid);
            Assert.Contains("CUSTOM_API_KEY", result.MissingRequiredSecrets);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CUSTOM_API_KEY", previousCustom);
        }
    }

    /// <summary>
    /// A suffixed Copilot entry gets the explicit --model pin and the BYOK provider tuple, so the
    /// attempt is not left on whatever server-side default Copilot serves that day.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_ProviderSuffixedCopilot_MapsModelAndByokTuple()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "copilot-custom", ImageKey = "copilot" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "openai",
                ["TESTMAP_LLM_MODEL"] = "GLM-5.3",
                ["TESTMAP_LLM_API_KEY"] = "custom-key",
                ["TESTMAP_LLM_BASE_URL"] = "https://llm-api.example.test/api/"
            }
        });

        Assert.Equal("GLM-5.3", env["COPILOT_MODEL"]);
        Assert.Equal("https://llm-api.example.test/api/", env["COPILOT_PROVIDER_BASE_URL"]);
        Assert.Equal("openai", env["COPILOT_PROVIDER_TYPE"]);
        Assert.Equal("custom-key", env["COPILOT_PROVIDER_API_KEY"]);
    }

    /// <summary>
    /// Every family's provider plumbing survives a suffixed id: the container env is identical to
    /// what the bare id produces.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("openhands", "LLM_MODEL")]
    [InlineData("claude", "CLAUDE_MODEL")]
    [InlineData("codex", "CODEX_MODEL")]
    [InlineData("copilot", "COPILOT_MODEL")]
    [InlineData("aider", "AIDER_MODEL")]
    [InlineData("mini-swe-agent", "MINI_MODEL")]
    [InlineData("gemini", "GEMINI_MODEL")]
    public void BuildContainerEnvironment_SuffixedIdMatchesBareId(string family, string modelVar)
    {
        var resolved = new Dictionary<string, string>
        {
            ["TESTMAP_LLM_PROVIDER"] = "anthropic",
            ["TESTMAP_LLM_MODEL"] = "claude-haiku-4-5-20251001",
            ["TESTMAP_LLM_API_KEY"] = "sk-ant"
        };

        var bare = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = family },
            ResolvedEnvironment = new Dictionary<string, string>(resolved)
        });

        var suffixed = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = $"{family}-anthropic", ImageKey = family },
            ResolvedEnvironment = new Dictionary<string, string>(resolved)
        });

        Assert.True(suffixed.ContainsKey(modelVar), $"{modelVar} was not set for {family}-anthropic.");
        Assert.Equal(bare[modelVar], suffixed[modelVar]);
    }

    /// <summary>
    /// Runner scripts name their artifacts after the family, so log paths for a suffixed entry
    /// must still point at copilot.events.jsonl rather than copilot-custom.events.jsonl.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveLogPaths_ProviderSuffixedCopilot_UsesFamilyNamedArtifacts()
    {
        var tool = new ExperimentToolConfig { Id = "copilot-custom", ImageKey = "copilot" };

        var paths = AgentToolLogPathResolver.Resolve("/artifacts", tool.Family);

        Assert.EndsWith("copilot.events.jsonl", paths.JsonlLogPath);
        Assert.EndsWith("copilot.stderr.log", paths.StdErrLogPath);
    }

    /// <summary>
    /// Token accounting and observed-model attribution read Copilot's OTEL stream. Both are gated
    /// on the family, so a suffixed entry still gets usage recorded instead of an empty attempt.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsageAndModel_ProviderSuffixedCopilot_ReadsOtelStream()
    {
        var artifacts = Directory.CreateTempSubdirectory("testmap-copilot-family-");
        try
        {
            var tool = new ExperimentToolConfig { Id = "copilot-custom", ImageKey = "copilot" };
            File.WriteAllText(
                Path.Combine(artifacts.FullName, "copilot-otel.jsonl"),
                """
                {"type":"span","name":"chat GLM-5.3","attributes":{"gen_ai.request.model":"GLM-5.3","gen_ai.response.model":"GLM-5.3"}}
                {"name":"gen_ai.client.token.usage","data":{"dataPoints":[{"sum":1200,"attributes":[{"key":"gen_ai.token.type","value":{"stringValue":"input"}}]},{"sum":340,"attributes":[{"key":"gen_ai.token.type","value":{"stringValue":"output"}}]}]}}
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifacts.FullName, tool.Family);
            var model = AgentToolEvaluationLane.ExtractObservedModel(artifacts.FullName, tool.Family);

            Assert.NotNull(usage);
            Assert.Equal(1200, usage.InputTokens);
            Assert.Equal(340, usage.OutputTokens);
            Assert.Equal("GLM-5.3", model);
        }
        finally
        {
            artifacts.Delete(recursive: true);
        }
    }
}
