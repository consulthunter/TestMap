using System.Configuration;
using Amazon;
using Amazon.BedrockRuntime;
using Microsoft.SemanticKernel;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Providers;

namespace TestMap.Services.Testing.Providers;

public class TestGenerator
{
    private readonly Kernel _kernel;

    public TestGenerator(TestMapConfig config)
    {
        var builder = Kernel.CreateBuilder();
        var provider = config.Generation.Provider;
        var model = config.Generation.Model;

        var openAiConfig = config.OpenAi;
        var ollamaConfig = config.Ollama;
        var googleConfig = config.Google;
        var amazonConfig = config.Amazon;
        var customConfig = config.Custom;

        switch (provider)
        {
            case "openai":
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(openAiConfig?.OrgId) ||
                    string.IsNullOrEmpty(openAiConfig?.ApiKey))
                    throw new ConfigurationErrorsException(
                        "For openai provider: OrgID, Model, and ApiKey must be configured.");
                builder.AddOpenAIChatCompletion(
                    model,
                    orgId: openAiConfig.OrgId,
                    apiKey: openAiConfig.ApiKey);
                break;

            case "ollama":
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(ollamaConfig?.Endpoint))
                    throw new ConfigurationErrorsException(
                        "For ollama provider: Model, and Endpoint must be configured.");
                builder.AddOllamaChatCompletion(
                    model,
                    new Uri(ollamaConfig.Endpoint));
                break;

            case "google":
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(googleConfig?.ApiKey))
                    throw new ConfigurationErrorsException(
                        "For google provider: Model, and ApiKey must be configured.");
                builder.AddGoogleAIGeminiChatCompletion(
                    model,
                    googleConfig.ApiKey);
                break;

            case "amazon":
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(amazonConfig?.AwsAccessKey)
                                                || string.IsNullOrEmpty(amazonConfig?.AwsSecretKey)
                                                || string.IsNullOrEmpty(amazonConfig?.AwsRegion))
                    throw new ConfigurationErrorsException(
                        "For anthropic provider: Model, AwsAccessKey, AwsSecretKey, and AwsRegion must be configured.");
                IAmazonBedrockRuntime runtime = new AmazonBedrockRuntimeClient(
                    amazonConfig.AwsAccessKey,
                    amazonConfig.AwsSecretKey,
                    GetAwsRegionEndpoint(amazonConfig.AwsRegion));
                builder.AddBedrockChatCompletionService(model, runtime);
                break;

            case "custom":
                var uri = new Uri(customConfig?.Endpoint ?? string.Empty);
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(customConfig?.OrgId) ||
                    string.IsNullOrEmpty(customConfig?.ApiKey))
                    throw new ConfigurationErrorsException(
                        "For openai provider: OrgID, Model, and ApiKey must be configured.");
                builder.AddOpenAIChatCompletion(
                    model,
                    uri,
                    orgId: customConfig.OrgId,
                    apiKey: customConfig.ApiKey);
                break;
            default:
                throw new InvalidOperationException($"Unsupported LLM provider: {provider}");
        }

        _kernel = builder.Build();
    }

    private static RegionEndpoint GetAwsRegionEndpoint(string region)
    {
        switch (region)
        {
            case "us-east-1":
                return RegionEndpoint.USEast1;
            case "us-east-2":
                return RegionEndpoint.USEast2;
            case "us-west-1":
                return RegionEndpoint.USWest1;
            case "us-west-2":
                return RegionEndpoint.USWest2;
            case "ca-central-1":
                return RegionEndpoint.CACentral1;
            case "mx-central-1":
                return RegionEndpoint.MXCentral1;
            default:
                throw new ConfigurationErrorsException($"Unsupported AWS region: {region}");
        }
    }

    public string CreateTestPrompt(string method, string test, string testFramework, string testDependencies)
    {
        return $"Here is a method that I'd like to test: {method}\n" +
               $"Here is a test from the same test class: {test}\n" +
               $"These are the dependencies for the test class: {testDependencies}\n" +
               $"Please write one test that will pass with good coverage using the {testFramework}\n" +
               $"Please add a doc-string to the test. \n" +
               $"Also comment the test with: // arrange, act, assert.\n" +
               $"I need just the test method not the test class." +
               $"Finally, delimit the code with ``` for the beginning and end of the block.";
    }

    public string CreateRepairTestPrompt(string method, string test, string testFramework, string testDependencies,
        string previousLogs)
    {
        return
            $"I previously attempted to generate a test for the following method:\n{method}\n\n" +
            $"Here is the test that was generated:\n{test}\n\n" +
            $"The test class dependencies are:\n{testDependencies}\n\n" +
            $"The previous test run produced the following logs:\n{previousLogs}\n\n" +
            $"Please write one **corrected test method** in {testFramework} that:\n" +
            "- Passes all tests\n" +
            "- Provides good coverage of the target method\n" +
            "- Includes a doc-string describing what the test does\n" +
            "- Uses comments: // arrange, // act, // assert\n\n" +
            $"Only provide the test method body, **not the entire class**, and delimit it with triple backticks ```.";
    }


    public async Task<string> CreateTest(string prompt)
    {
        return await _kernel.InvokePromptAsync<string>(prompt) ?? string.Empty;
    }
}