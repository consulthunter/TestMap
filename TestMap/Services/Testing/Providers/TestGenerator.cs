
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TestMap.Models.Configuration;

namespace TestMap.Services.Testing.Providers;

public class TestGenerator
{
    private readonly Kernel _kernel;

    public TestGenerator(TestMapConfig config)
    {
        var builder = Kernel.CreateBuilder();
        string provider = config.Generation.Provider;
        string model = config.Generation.Model;
        string orgid = config.Generation.OrgId;
        string apiKey = config.Generation.ApiKey;
        string endpoint = config.Generation.Endpoint;
        
        switch (provider)
        {
            case "openai":
                builder.AddOpenAIChatCompletion(
                    modelId: model,
                    orgId: orgid,
                    apiKey: apiKey);
                break;

            case "ollama":
                builder.AddOllamaChatCompletion(
                    modelId: model,
                    endpoint: new Uri(endpoint));
                break;
            
            case "google":
                builder.AddGoogleAIGeminiChatCompletion(
                    modelId: model,  
                    apiKey: apiKey);
                break;

            default:
                throw new InvalidOperationException($"Unsupported LLM provider: {provider}");
        }

        _kernel = builder.Build();
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
               $"Finally, delimit the code with ``` for the beginning and end of the block." ;
    }

    public string CreateRepairTestPrompt(string method, string test, string testFramework, string testDependencies, string previousLogs)
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