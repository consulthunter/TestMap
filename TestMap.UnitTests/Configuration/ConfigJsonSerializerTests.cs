using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Configuration;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.UnitTests.Configuration;

public sealed class ConfigJsonSerializerTests
{
    private readonly JsonSerializerOptions _options;

    public ConfigJsonSerializerTests()
    {
        _options = ConfigJsonSerializer.CreateOptions();
    }

    /// <summary>
    /// Verifies that the configuration serializer writes friendly kebab-case names for supported enum values.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(AiProvider.OpenAi, "\"openai\"")]
    [InlineData(AiProvider.CustomOpenAi, "\"custom-openai\"")]
    [InlineData(AiProvider.GoogleGemini, "\"google-gemini\"")]
    [InlineData(TargetSelectionStrategy.MetricDrivenImprovement, "\"metric-driven-improvement\"")]
    [InlineData(TestActionExecutorMode.BasicExtension, "\"basic-extension\"")]
    [InlineData(AiProviderMode.Chat, "\"chat\"")]
    [InlineData(TestGenerationObjective.TestSuiteExpansion, "\"test-suite-expansion\"")]
    [InlineData(MetricsDrivenPath.CoverageAndMutation, "\"coverage-and-mutation\"")]
    [InlineData(GenerationContextMode.ChainedHistory, "\"chained-history\"")]
    [InlineData(GenerationBudgetMode.PassAt1RepairAt5, "\"pass-at1-repair-at5\"")]
    [InlineData(GenerationStepStatus.Fallback, "\"fallback\"")]
    public void Serialize_EnumValue_WritesFriendlyAlias<TEnum>(TEnum value, string expectedJson)
        where TEnum : struct, Enum
    {
        // Arrange
        var input = value;

        // Act
        var json = JsonSerializer.Serialize(input, _options);

        // Assert
        Assert.Equal(expectedJson, json);
    }

    /// <summary>
    /// Verifies that the configuration serializer accepts enum values written in multiple supported name formats.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("\"OpenAi\"", AiProvider.OpenAi)]
    [InlineData("\"open-ai\"", AiProvider.OpenAi)]
    [InlineData("\"openai\"", AiProvider.OpenAi)]
    [InlineData("\"CustomOpenAi\"", AiProvider.CustomOpenAi)]
    [InlineData("\"custom-openai\"", AiProvider.CustomOpenAi)]
    [InlineData("\"MetricDrivenImprovement\"", TargetSelectionStrategy.MetricDrivenImprovement)]
    [InlineData("\"metric-driven-improvement\"", TargetSelectionStrategy.MetricDrivenImprovement)]
    [InlineData("\"ActionAware\"", TestGenerationApproach.ActionAware)]
    [InlineData("\"action-aware\"", TestGenerationApproach.ActionAware)]
    [InlineData("\"MetricsDriven\"", TestGenerationApproach.MetricsDriven)]
    [InlineData("\"metrics-driven\"", TestGenerationApproach.MetricsDriven)]
    [InlineData("\"BasicExtension\"", TestActionExecutorMode.BasicExtension)]
    [InlineData("\"basic-extension\"", TestActionExecutorMode.BasicExtension)]
    [InlineData("\"CoverageAndMutation\"", MetricsDrivenPath.CoverageAndMutation)]
    [InlineData("\"coverage-and-mutation\"", MetricsDrivenPath.CoverageAndMutation)]
    [InlineData("\"NoHistory\"", GenerationContextMode.NoHistory)]
    [InlineData("\"no-history\"", GenerationContextMode.NoHistory)]
    [InlineData("\"PassAt5\"", GenerationBudgetMode.PassAt5)]
    [InlineData("\"pass-at5\"", GenerationBudgetMode.PassAt5)]
    [InlineData("\"NotApplicable\"", GenerationStepStatus.NotApplicable)]
    [InlineData("\"not-applicable\"", GenerationStepStatus.NotApplicable)]
    public void Deserialize_EnumValue_ReadsSupportedAliases<TEnum>(string json, TEnum expectedValue)
        where TEnum : struct, Enum
    {
        // Arrange
        var input = json;

        // Act
        var value = JsonSerializer.Deserialize<TEnum>(input, _options);

        // Assert
        Assert.Equal(expectedValue, value);
    }

    /// <summary>
    /// Verifies that the configuration serializer supports numeric enum values when deserializing.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("0", AiProvider.Amazon)]
    [InlineData("5", AiProvider.OpenAi)]
    [InlineData("0", AiProviderMode.Chat)]
    [InlineData("1", AiProviderMode.Inference)]
    public void Deserialize_NumericEnumValue_ReadsEnumMember<TEnum>(string json, TEnum expectedValue)
        where TEnum : struct, Enum
    {
        // Arrange
        var input = json;

        // Act
        var value = JsonSerializer.Deserialize<TEnum>(input, _options);

        // Assert
        Assert.Equal(expectedValue, value);
    }

    /// <summary>
    /// Verifies that the configuration serializer rejects unknown enum aliases with a useful failure.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("\"not-a-provider\"")]
    [InlineData("\"\"")]
    public void Deserialize_InvalidEnumValue_ThrowsJsonException(string json)
    {
        // Arrange
        var input = json;

        // Act
        Action action = () => JsonSerializer.Deserialize<AiProvider>(input, _options);

        // Assert
        Assert.Throws<JsonException>(action);
    }

    /// <summary>
    /// Verifies that the serializer materializes interface-based test framework configuration objects.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_TestMapConfig_ReadsFrameworkConfigsIntoConcreteInstances()
    {
        // Arrange
        const string json = """
                            {
                              "TestingConfig": {
                                "TestingFrameworks": [
                                  {
                                    "patterns": ["Fact", "Theory"]
                                  }
                                ]
                              }
                            }
                            """;

        // Act
        var config = JsonSerializer.Deserialize<TestMapConfig>(json, _options);

        // Assert
        Assert.NotNull(config);
        var framework = Assert.Single(config.TestingConfig.TestingFrameworks);
        var concreteFramework = Assert.IsType<FrameworkConfig>(framework);
        Assert.Equal(["Fact", "Theory"], concreteFramework.patterns);
    }

    /// <summary>
    /// Verifies that the serializer writes interface-based framework configuration using the expected JSON shape.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Serialize_TestMapConfig_WritesFrameworkConfigsUsingPatternsProperty()
    {
        // Arrange
        var config = new TestMapConfig
        {
            TestingConfig = new TestingConfig
            {
                TestingFrameworks =
                [
                    new FrameworkConfig
                    {
                        patterns = ["Test", "Theory"]
                    }
                ]
            }
        };

        // Act
        var json = JsonSerializer.Serialize(config, _options);

        // Assert
        Assert.Contains("\"patterns\": [", json);
        Assert.Contains("\"Test\"", json);
        Assert.Contains("\"Theory\"", json);
    }
}
