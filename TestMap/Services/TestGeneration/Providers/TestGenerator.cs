using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.TestGeneration.Providers;

public class TestGenerator
{
    private readonly TestMapConfig _config;
    private readonly Dictionary<AiProvider, IAiGenerationProvider> _providers;

    public TestGenerator(TestMapConfig config, IEnumerable<IAiGenerationProvider> providers)
    {
        _config = config;
        _providers = providers.ToDictionary(x => x.Provider);
    }

    public string CreateTestPrompt(string method, string test, string testClass, string testFramework,
        string testDependencies)
    {
        return $"Here is a method that I'd like to test: {method}\n" +
               $"Here is a test from the same test class that covers this method: {test}\n" +
               $"This test is part of the class: {testClass}\n" +
               $"These are the dependencies for the test class: {testDependencies}\n" +
               $"Please write one test that will pass and extend coverage using the {testFramework}\n" +
               "Please add a doc-string to the test. \n" +
               "Also comment the test with: // arrange, act, assert.\n" +
               "I need just the test method not the test class." +
               "Finally, delimit the code with ``` for the beginning and end of the block.";
    }

    public string CreateRepairTestPrompt(
        string method,
        string test,
        string testClass,
        string testFramework,
        string testDependencies,
        string previousLogs)
    {
        return
            $"I previously attempted to generate a test for the following method:\n{method}\n\n" +
            $"Here is the test that was generated:\n{test}\n\n" +
            $"This test is part of the class: {testClass}\n\n" +
            $"The test class dependencies are:\n{testDependencies}\n\n" +
            $"The previous test run produced the following logs:\n{previousLogs}\n\n" +
            $"Please write one corrected test method in {testFramework} that:\n" +
            "- Compiles and Passes\n" +
            "- Extends good coverage of the target method\n" +
            "- Includes a doc-string describing what the test does\n" +
            "- Uses comments: // arrange, // act, // assert\n\n" +
            "Only provide the test method body, not the entire class, and delimit it with triple backticks ```.";
    }

    public async Task<string> CreateTest(string prompt, double temperature = 0.0,
        CancellationToken cancellationToken = default)
    {
        var generationConfig = _config.TestingConfig.GenerationConfig;
        var providerConfig = _config.AiProviderConfig.GetProviderConfig(generationConfig.Provider)
                             ?? throw new InvalidOperationException(
                                 $"Provider config not found for {generationConfig.Provider}.");

        if (!_providers.TryGetValue(generationConfig.Provider, out var provider))
            throw new InvalidOperationException(
                $"Provider implementation not registered for {generationConfig.Provider}.");

        await provider.CreateAsync(providerConfig, generationConfig.Mode, cancellationToken);
        return await provider.GenerateAsync(prompt, temperature, cancellationToken);
    }
}