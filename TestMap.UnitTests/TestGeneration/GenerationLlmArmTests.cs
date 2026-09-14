using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Configuration;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.TestGeneration;

/// <summary>
/// Tests for ExperimentConfig.LlmArms: several generation-lane arms sharing one provider and
/// differing only by model, so a run compares open models without being repeated per model.
/// Also pins the backward-compatible path — a config with no arms must expand and key exactly as
/// it did before arms existed.
/// </summary>
public sealed class GenerationLlmArmTests
{
    private static TestMapConfig MakeConfig()
    {
        var config = new TestMapConfig();
        config.AiProviderConfig.OpenAi.Model = "gpt-test";
        config.AiProviderConfig.CustomOpenAi.Model = "GLM-5.3";
        config.AiProviderConfig.CustomOpenAi.ApiKey = "custom-key";
        config.AiProviderConfig.CustomOpenAi.Endpoint = "https://llm-api.example.test/api/";
        return config;
    }

    private static ExperimentConfig MakeExperimentConfig(params ExperimentLlmArmConfig[] arms) =>
        new()
        {
            Approaches = [TestGenerationApproach.MetricsDriven],
            MetricsPaths = [MetricsDrivenPath.CoverageAndMutation],
            BudgetModes = [GenerationBudgetMode.PassAt1],
            ContextModes = [GenerationContextMode.ChainedHistory],
            LlmArms = arms.ToList()
        };

    private static GenerationExperimentMatrixGenerator MakeGenerator(TestMapConfig config) =>
        new(config, new StepAblationVariantGenerator());

    /// <summary>
    /// The case this exists for: three open models behind one custom endpoint, expanded into one
    /// matrix instead of three experiment runs.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_SeveralModelsOnOneProvider_ProducesOneItemPerModel()
    {
        var generator = MakeGenerator(MakeConfig());

        var matrix = generator.Generate(
            MakeExperimentConfig(),
            [
                new GenerationLlmArm { Id = "custom-glm", Provider = AiProvider.CustomOpenAi, ModelName = "GLM-5.3" },
                new GenerationLlmArm { Id = "custom-oss", Provider = AiProvider.CustomOpenAi, ModelName = "gpt-oss-120b" },
                new GenerationLlmArm { Id = "custom-kimi", Provider = AiProvider.CustomOpenAi, ModelName = "Kimi-K3" }
            ]);

        Assert.Equal(3, matrix.Items.Count);
        Assert.All(matrix.Items, x => Assert.Equal(AiProvider.CustomOpenAi, x.Provider));
        Assert.Equal(
            ["GLM-5.3", "gpt-oss-120b", "Kimi-K3"],
            matrix.Items.Select(x => x.ModelName).ToArray());
        Assert.Equal(3, matrix.Items.Select(x => x.VariantId).Distinct().Count());
        Assert.All(matrix.Items, x => Assert.StartsWith(x.ArmId, x.VariantId));
    }

    /// <summary>
    /// An arm with no model of its own falls back to the provider's configured model.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ArmWithoutModel_FallsBackToProviderModel()
    {
        var generator = MakeGenerator(MakeConfig());

        var matrix = generator.Generate(
            MakeExperimentConfig(),
            [new GenerationLlmArm { Id = "openai-default", Provider = AiProvider.OpenAi }]);

        Assert.Equal("gpt-test", Assert.Single(matrix.Items).ModelName);
    }

    /// <summary>
    /// A config that declares no arms must expand exactly as it did before arms existed — same
    /// count, same variant ids — so prior results and resume state stay valid.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_NoArms_MatchesLegacyProviderExpansion()
    {
        var generator = MakeGenerator(MakeConfig());
        var config = MakeExperimentConfig();

        var legacy = generator.Generate(config, new[] { AiProvider.OpenAi });

        var item = Assert.Single(legacy.Items);
        Assert.Equal("OpenAi", item.ArmId);
        Assert.Equal("gpt-test", item.ModelName);
        Assert.StartsWith("OpenAi__MetricsDriven__", item.VariantId);
        Assert.Null(item.Endpoint);
    }

    /// <summary>
    /// Two arms differing only by endpoint would otherwise collide on the resume stable key and
    /// one of them would be skipped as already done.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildStableKey_ArmsDifferingOnlyByEndpoint_AreDistinct()
    {
        var service = new ExperimentResumeService();
        var candidate = new CandidateMethod { MemberId = 7 };

        string Key(string armId, string endpoint) => service.BuildStableKey(
            "run",
            "owner/repo",
            "abc",
            TestGenerationObjective.TestSuiteExpansion,
            candidate,
            new GenerationExperimentMatrixItem
            {
                VariantId = "v",
                ArmId = armId,
                Provider = AiProvider.CustomOpenAi,
                ModelName = "gpt-oss-120b",
                Endpoint = endpoint,
                Approach = TestGenerationApproach.MetricsDriven,
                ContextMode = GenerationContextMode.ChainedHistory,
                BudgetMode = GenerationBudgetMode.PassAt1,
                Steps = new GenerationStepConfig { VariantId = "baseline" },
                Temperature = 0
            });

        Assert.NotEqual(Key("oss-a", "https://a.test/"), Key("oss-b", "https://b.test/"));
    }

    /// <summary>
    /// The stable key for an arm-free config is byte-identical to the pre-arms key, which is what
    /// keeps an interrupted run resumable across this change.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildStableKey_ProviderNamedArm_MatchesKeyWithoutArmId()
    {
        var service = new ExperimentResumeService();
        var candidate = new CandidateMethod { MemberId = 7 };

        GenerationExperimentMatrixItem Item(string armId) => new()
        {
            VariantId = "v",
            ArmId = armId,
            Provider = AiProvider.OpenAi,
            ModelName = "gpt-test",
            Approach = TestGenerationApproach.MetricsDriven,
            ContextMode = GenerationContextMode.ChainedHistory,
            BudgetMode = GenerationBudgetMode.PassAt1,
            Steps = new GenerationStepConfig { VariantId = "baseline" },
            Temperature = 0
        };

        string Key(GenerationExperimentMatrixItem item) => service.BuildStableKey(
            "run", "owner/repo", "abc", TestGenerationObjective.TestSuiteExpansion, candidate, item);

        Assert.Equal(Key(Item(string.Empty)), Key(Item("OpenAi")));
        Assert.NotEqual(Key(Item("OpenAi")), Key(Item("openai-alt")));
    }

    /// <summary>
    /// Overrides must never touch the shared provider config: it is a singleton every later
    /// attempt in the run reads through, so an in-place write would pin every subsequent arm to
    /// the first arm's model.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_ModelOverride_ClonesAndLeavesSourceUntouched()
    {
        var source = new CustomOpenAiConfig
        {
            Model = "GLM-5.3",
            ApiKey = "custom-key",
            OrgId = "org-1",
            Endpoint = "https://llm-api.example.test/api/"
        };

        var overridden = AiProviderConfigOverrides.Apply(source, "gpt-oss-120b", null);

        Assert.NotSame(source, overridden);
        Assert.Equal("GLM-5.3", source.Model);
        Assert.Equal("gpt-oss-120b", overridden.Model);
        // Provider-specific fields must survive the clone or the attempt loses its credentials.
        var clone = Assert.IsType<CustomOpenAiConfig>(overridden);
        Assert.Equal("custom-key", clone.ApiKey);
        Assert.Equal("org-1", clone.OrgId);
        Assert.Equal("https://llm-api.example.test/api/", clone.Endpoint);
        Assert.Equal(AiProvider.CustomOpenAi, clone.Provider);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_EndpointOverride_ReplacesEndpointOnClone()
    {
        var source = new CustomOpenAiConfig { Model = "m", Endpoint = "https://a.test/" };

        var clone = Assert.IsType<CustomOpenAiConfig>(
            AiProviderConfigOverrides.Apply(source, null, "https://b.test/"));

        Assert.Equal("https://b.test/", clone.Endpoint);
        Assert.Equal("https://a.test/", source.Endpoint);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_NoOverrides_ReturnsSourceUnchanged()
    {
        var source = new CustomOpenAiConfig { Model = "m", Endpoint = "https://a.test/" };

        Assert.Same(source, AiProviderConfigOverrides.Apply(source, null, null));
        Assert.Same(source, AiProviderConfigOverrides.Apply(source, "m", "https://a.test/"));
    }

    /// <summary>
    /// An endpoint override on a provider that has no endpoint is a config mistake, and silently
    /// dropping it would run the arm against the wrong host.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_EndpointOverrideOnProviderWithoutEndpoint_Throws()
    {
        var source = new TestMapConfig().AiProviderConfig.Anthropic;
        source.Model = "claude-haiku-4-5-20251001";

        var exception = Assert.Throws<InvalidOperationException>(
            () => AiProviderConfigOverrides.Apply(source, null, "https://a.test/"));

        Assert.Contains("no endpoint to override", exception.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_ArmsAlongsideIncludeProviders_Throws()
    {
        var config = MakeExperimentConfig(new ExperimentLlmArmConfig { Id = "a", Model = "m" });
        config.IncludeProviders = ["OpenAi"];

        var exception = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));

        Assert.Contains("replaces IncludeProviders", exception.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_ArmsAlongsidePreferredProvider_Throws()
    {
        var config = MakeExperimentConfig(new ExperimentLlmArmConfig { Id = "a", Model = "m" });
        config.PreferredProvider = "OpenAi";

        Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_DuplicateArmId_Throws()
    {
        var config = MakeExperimentConfig(
            new ExperimentLlmArmConfig { Id = "custom", Provider = AiProvider.CustomOpenAi, Model = "a" },
            new ExperimentLlmArmConfig { Id = "custom", Provider = AiProvider.CustomOpenAi, Model = "b" });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));

        Assert.Contains("must be unique", exception.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_ArmsResolvingToSameModel_Throws()
    {
        var config = MakeExperimentConfig(
            new ExperimentLlmArmConfig { Id = "one", Provider = AiProvider.CustomOpenAi, Model = "GLM-5.3" },
            new ExperimentLlmArmConfig { Id = "two", Provider = AiProvider.CustomOpenAi, Model = "GLM-5.3" });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));

        Assert.Contains("same provider, model and endpoint", exception.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_EmptyArmId_Throws()
    {
        var config = MakeExperimentConfig(new ExperimentLlmArmConfig { Id = "  ", Model = "m" });

        Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));
    }

    /// <summary>
    /// A config with no arms must pass validation untouched — this is every config in the repo
    /// today.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_NoArmsWithIncludeProviders_Passes()
    {
        var config = MakeExperimentConfig();
        config.IncludeProviders = ["OpenAi"];
        config.PreferredProvider = "OpenAi";

        ExperimentConfigurationValidator.ValidateMatrixSettings(config);
    }
}
