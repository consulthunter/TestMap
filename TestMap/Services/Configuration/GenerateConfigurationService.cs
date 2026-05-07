/*
 * consulthunter
 * 2025-03-26
 *
 * Generates the config for TestMap
 *
 * GenerateConfigurationService.cs
 */

using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders.Amazon;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Models.Configuration.AiProviders.Ollama;
using TestMap.Models.Configuration.AiProviders.OpenAI;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Framework;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.Configuration;

public class GenerateConfigurationService(string configurationFilePath, string basePath, string basePathParent)
{
    public void GenerateConfiguration()
    {
        var config = new TestMapConfig();

        // Runtime Configuration
        config.RuntimeConfig.FilePaths.TargetFilePath = Path.Combine(basePath, "Data", "example_project.txt");
        config.RuntimeConfig.FilePaths.LogsDirPath = Path.Combine(basePath, "Logs");
        config.RuntimeConfig.FilePaths.TempDirPath = Path.Combine(basePathParent, "Temp");
        config.RuntimeConfig.FilePaths.OutputDirPath = Path.Combine(basePath, "Output");

        config.RuntimeConfig.MaxConcurrency = 5;
        config.RuntimeConfig.RunDateFormat = "yyyy-MM-dd";

        config.RuntimeConfig.Docker.Context = "desktop-linux";
        config.RuntimeConfig.Docker.Image = "net-sdk-all";

        config.RuntimeConfig.Frameworks = new Dictionary<string, List<string>>
        {
            ["NUnit"] = new() { "Test", "TestCase", "TestCaseSource", "Theory" },
            ["xUnit"] = new() { "Fact", "Theory" },
            ["MSTest"] = new() { "TestMethod", "DataSource" },
            ["Microsoft.VisualStudio.TestTools.UnitTesting"] = new() { "TestMethod", "DataSource" }
        };

        config.RuntimeConfig.Project.KeepProjectFiles = true;

        // Testing Configuration
        config.TestingConfig.GenerationConfig.Objective = TestGenerationObjective.TestSuiteExpansion;
        config.TestingConfig.GenerationConfig.Provider = AiProvider.OpenAi;
        config.TestingConfig.GenerationConfig.Mode = AiProviderMode.Chat;
        config.TestingConfig.GenerationConfig.Strategy = TestGenerationApproach.MetricsDriven;
        config.TestingConfig.GenerationConfig.MetricsPath = MetricsDrivenPath.CoverageAndMutation;
        config.TestingConfig.GenerationConfig.Executor = TestActionExecutorMode.BasicExtension;
        config.TestingConfig.GenerationConfig.BudgetMode = GenerationBudgetMode.PassAt1RepairAt5;
        config.TestingConfig.GenerationConfig.Temperature = 0.0;
        config.TestingConfig.GenerationConfig.StepErrorRetries = 0;
        config.TestingConfig.GenerationConfig.StepRetryDelayMs = 1000;
        config.TestingConfig.GenerationConfig.ContextMode = GenerationContextMode.ChainedHistory;
        config.TestingConfig.GenerationConfig.Steps.EnableRoslynValidation = true;

        config.TestingConfig.TestingFrameworks.Add(new NunitConfig
            { patterns = new List<string> { "Test", "TestCase", "TestCaseSource", "Theory" } });
        config.TestingConfig.TestingFrameworks.Add(new XunitConfig
            { patterns = new List<string> { "Fact", "Theory" } });
        config.TestingConfig.TestingFrameworks.Add(new MsTestConfig
            { patterns = new List<string> { "TestMethod", "DataSource" } });

        // AI Provider Configuration
        config.AiProviderConfig.OpenAi = new OpenAiConfig
        {
            Provider = AiProvider.OpenAi,
            Model = "gpt-3.5-turbo"
        };

        config.AiProviderConfig.Amazon = new AmazonConfig
        {
            Provider = AiProvider.Amazon,
            AwsRegion = "us-east-1"
        };

        config.AiProviderConfig.Ollama = new OllamaConfig
        {
            Provider = AiProvider.Ollama,
            Endpoint = "http://localhost:10000/"
        };

        config.AiProviderConfig.CustomOpenAi = new CustomOpenAiConfig
        {
            Provider = AiProvider.CustomOpenAi,
            Endpoint = "http://localhost:11434/"
        };

        config.AiProviderConfig.GoogleGemini = new GoogleGeminiConfig
        {
            Provider = AiProvider.GoogleGemini,
            Model = "gemini-1.5-flash"
        };

        config.AiProviderConfig.GoogleCloud = new GoogleCloudConfig
        {
            Provider = AiProvider.GoogleCloud,
            Model = "gemini-1.5-flash",
            Location = "us-central1"
        };

        // Experiment Configuration
        config.ExperimentConfig.Objective = TestGenerationObjective.TestSuiteExpansion;
        config.ExperimentConfig.CandidateSelectionStrategy = TargetSelectionStrategy.MetricDrivenImprovement;
        config.ExperimentConfig.GenerationApproach = TestGenerationApproach.MetricsDriven;
        config.ExperimentConfig.Executor = TestActionExecutorMode.BasicExtension;
        config.ExperimentConfig.Approaches =
        [
            TestGenerationApproach.Naive,
            TestGenerationApproach.MetricsDriven
        ];
        config.ExperimentConfig.MetricsPaths =
        [
            MetricsDrivenPath.Coverage,
            MetricsDrivenPath.Mutation,
            MetricsDrivenPath.CoverageAndMutation
        ];
        config.ExperimentConfig.BudgetModes =
        [
            GenerationBudgetMode.PassAt1,
            GenerationBudgetMode.PassAt5
        ];
        config.ExperimentConfig.CompareHistoryModes = true;
        config.ExperimentConfig.ContextModes =
        [
            GenerationContextMode.ChainedHistory
        ];
        config.ExperimentConfig.StepAblation = new StepAblationConfig
        {
            Enabled = false,
            IncludeBaseline = true,
            IncludeAllDisabled = false,
            MaxVariants = 32,
            Steps =
            [
                GenerationStepType.EvidencePackage,
                GenerationStepType.ContextGraph,
                GenerationStepType.ContextResolution,
                GenerationStepType.RoslynValidation,
                GenerationStepType.Scenario,
                GenerationStepType.MethodName,
                GenerationStepType.ArrangePlan,
                GenerationStepType.InputPlan,
                GenerationStepType.ActionPlan,
                GenerationStepType.AssertionPlan,
                GenerationStepType.FinalTest
            ]
        };
        config.ExperimentConfig.Temperature = 0.0;
        config.ExperimentConfig.CandidateLimit = 3;
        config.ExperimentConfig.MinCoverageThreshold = 0.0;
        config.ExperimentConfig.MaxCoverageThreshold = 0.99;
        config.ExperimentConfig.OutputPath = Path.Combine(basePath, "Output");
        config.ExperimentConfig.IncludeDetailedErrors = true;
        config.ExperimentConfig.StepErrorRetries = 0;
        config.ExperimentConfig.StepRetryDelayMs = 1000;
        config.ExperimentConfig.Resume = new ExperimentResumeConfig
        {
            Enabled = true,
            RunningAttemptTimeoutMinutes = 60,
            RewriteResultsFileOnResume = false
        };

        // Use System.Text.Json for serialization
        var options = ConfigJsonSerializer.CreateOptions();

        // Serialize the config data to a JSON string
        var json = JsonSerializer.Serialize(config, options);

        // Write the JSON configuration to the file
        File.WriteAllText(configurationFilePath, json);
    }
}
