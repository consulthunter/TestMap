using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Anthropic;
using TestMap.Models.Configuration.AiProviders.OpenAI;
using TestMap.Services.TestGeneration.Providers.Abstractions;
using TestMap.Services.TestGeneration.Providers.Anthropic;

// Temperature is obsolete in the SDK; these tests read it to pin when the provider sets it.
#pragma warning disable CS0618

namespace TestMap.UnitTests.TestGeneration;

/// <summary>
/// Tests for the direct Anthropic SDK provider. Pins the request shape (what is and is not sent)
/// and the token-estimate segments, which must match what the request actually carries.
/// </summary>
public sealed class AnthropicGenerationProviderTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("claude-haiku-4-5", true)]
    [InlineData("claude-sonnet-4-6", true)]
    [InlineData("claude-opus-4-6", true)]
    [InlineData("claude-opus-4-7", false)]
    [InlineData("claude-opus-4-8", false)]
    [InlineData("claude-opus-5", false)]
    [InlineData("claude-sonnet-5", false)]
    [InlineData("claude-fable-5-1", false)]
    public void AcceptsSamplingParameters_FollowsModelFamily(string model, bool expected)
    {
        Assert.Equal(expected, AnthropicGenerationProvider.AcceptsSamplingParameters(model));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildRequest_WithoutTemperature_LeavesItUnset()
    {
        var request = AnthropicGenerationProvider.BuildRequest("claude-opus-5", "prompt", "system", null);

        Assert.Null(request.Temperature);
        Assert.NotNull(request.System);
        Assert.Equal(AnthropicGenerationProvider.MaxOutputTokens, (int)request.MaxTokens);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildRequest_WithoutSystemPrompt_SendsTemperatureOnly()
    {
        var request = AnthropicGenerationProvider.BuildRequest("claude-haiku-4-5", "prompt", null, 0.7);

        Assert.Equal<double?>(0.7, request.Temperature);
        Assert.Null(request.System);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_WithAnotherProvidersConfig_Throws()
    {
        var provider = new AnthropicGenerationProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CreateAsync(new OpenAiConfig { Model = "gpt" }, AiProviderMode.Chat));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_WithoutModel_Throws()
    {
        var provider = new AnthropicGenerationProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CreateAsync(new AnthropicConfig { ApiKey = "key" }, AiProviderMode.Chat));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetTokenizableInputSegments_MatchesWhatEachModeSends()
    {
        var provider = new AnthropicGenerationProvider();
        var config = new AnthropicConfig { Model = "claude-haiku-4-5", ApiKey = "key" };

        await provider.CreateAsync(config, AiProviderMode.Chat);
        Assert.Equal(
            [SemanticKernelGenerationProviderBase.DefaultSystemPrompt, "prompt"],
            provider.GetTokenizableInputSegments("prompt"));

        await provider.CreateAsync(config, AiProviderMode.Inference);
        Assert.Equal(["prompt"], provider.GetTokenizableInputSegments("prompt"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Provider_IsAnthropic()
    {
        Assert.Equal(AiProvider.Anthropic, new AnthropicGenerationProvider().Provider);
    }
}
