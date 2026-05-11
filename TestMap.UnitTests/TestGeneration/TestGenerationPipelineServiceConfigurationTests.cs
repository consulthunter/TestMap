using TestMap.App;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.UnitTests.TestGeneration;

public sealed class TestGenerationPipelineServiceConfigurationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateTestAsync_DisabledPlanningSteps_RecordsFallbackMetadata()
    {
        var provider = new RecordingProvider([
            "```csharp\n[Fact]\npublic void Add_GeneratedScenario_ExpectedBehavior()\n{\n}\n```"
        ]);
        var service = CreateService(provider);
        var request = CreateRequest(steps: new GenerationStepConfig
        {
            EnableScenario = false,
            EnableMethodName = false,
            EnableArrangePlan = false,
            EnableInputPlan = false,
            EnableActionPlan = false,
            EnableAssertionPlan = false
        });

        var result = await service.GenerateTestAsync(request);

        Assert.True(result.Success);
        Assert.Single(provider.Prompts);
        Assert.Contains(result.Steps, x => x.StepType == GenerationStepType.Scenario &&
                                           x.Status == GenerationStepStatus.Fallback);
        Assert.Contains(result.Steps, x => x.StepType == GenerationStepType.MethodName &&
                                           x.Status == GenerationStepStatus.Fallback);
        Assert.Contains(result.Steps, x => x.StepType == GenerationStepType.ArrangePlan &&
                                           x.Status == GenerationStepStatus.Fallback);
        Assert.Contains(result.Steps, x => x.StepType == GenerationStepType.FinalTest &&
                                           x.Status == GenerationStepStatus.Executed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateTestAsync_NoHistory_DoesNotIncludeConversationTranscriptInLaterPrompts()
    {
        var provider = new RecordingProvider([
            "scenario response",
            "Add_WhenInputsProvided_ReturnsSum",
            "```csharp\n[Fact]\npublic void Add_WhenInputsProvided_ReturnsSum()\n{\n}\n```"
        ]);
        var service = CreateService(provider);
        var request = CreateRequest(
            contextMode: GenerationContextMode.NoHistory,
            steps: PlanningDisabledExceptScenarioAndMethodName());

        var result = await service.GenerateTestAsync(request);

        Assert.True(result.Success);
        Assert.Equal(3, provider.Prompts.Count);
        Assert.DoesNotContain("Conversation so far:", provider.Prompts[2]);
        Assert.Null(result.ConversationTranscript);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateTestAsync_ChainedHistory_IncludesConversationTranscriptInLaterPrompts()
    {
        var provider = new RecordingProvider([
            "scenario response",
            "Add_WhenInputsProvided_ReturnsSum",
            "```csharp\n[Fact]\npublic void Add_WhenInputsProvided_ReturnsSum()\n{\n}\n```"
        ]);
        var service = CreateService(provider);
        var request = CreateRequest(
            contextMode: GenerationContextMode.ChainedHistory,
            steps: PlanningDisabledExceptScenarioAndMethodName());

        var result = await service.GenerateTestAsync(request);

        Assert.True(result.Success);
        Assert.Equal(3, provider.Prompts.Count);
        Assert.Contains("Conversation so far:", provider.Prompts[2]);
        Assert.Contains("[Scenario] Assistant:", provider.Prompts[2]);
        Assert.NotNull(result.ConversationTranscript);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateTestAsync_ContextGraphEnabled_AddsContextMetadataAndPromptHints()
    {
        var provider = new RecordingProvider([
            "```json\n{\"dependencies\":[],\"requiredNamespaces\":[],\"helperReuse\":[],\"requiresMocks\":false,\"arrangeStrategy\":\"use context\"}\n```",
            "```csharp\n[Fact]\npublic void Add_WhenInputsProvided_ReturnsSum()\n{\n}\n```"
        ]);
        var service = CreateService(provider);
        var request = CreateRequest(steps: new GenerationStepConfig
        {
            EnableContextGraph = true,
            EnableContextResolution = true,
            EnableScenario = false,
            EnableMethodName = false,
            EnableArrangePlan = true,
            EnableInputPlan = false,
            EnableActionPlan = false,
            EnableAssertionPlan = false
        });

        var result = await service.GenerateTestAsync(request);

        Assert.True(result.Success);
        Assert.Contains(result.Steps, x => x.StepType == GenerationStepType.ContextGraph &&
                                           x.StructuredResponseJson != null);
        Assert.Contains(result.Steps, x => x.StepType == GenerationStepType.ContextResolution &&
                                           x.StructuredResponseJson != null);
        Assert.Contains("Context graph and resolution hints:", provider.Prompts[0]);
        Assert.Contains("MethodParameter", provider.Prompts[0]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateTestAsync_MutationMetricsPath_IncludesMutationContextInPrompt()
    {
        var provider = new RecordingProvider([
            "scenario response",
            "Add_WhenInputsProvided_ReturnsSum",
            "```csharp\n[Fact]\npublic void Add_WhenInputsProvided_ReturnsSum()\n{\n}\n```"
        ]);
        var service = CreateService(provider);
        var request = CreateRequest(
            metricsPath: MetricsDrivenPath.Mutation,
            mutationSummary: "Mutation evidence to target:\n- Mutant 1: Survived, EqualityOperator, lines 3-3, replacement=`>=`",
            steps: PlanningDisabledExceptScenarioAndMethodName());

        var result = await service.GenerateTestAsync(request);

        Assert.True(result.Success);
        Assert.Contains("Mutation context:", provider.Prompts[0]);
        Assert.Contains("EqualityOperator", provider.Prompts[0]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateTestAsync_CoverageMetricsPath_OmitsMutationContextFromPrompt()
    {
        var provider = new RecordingProvider([
            "scenario response",
            "Add_WhenInputsProvided_ReturnsSum",
            "```csharp\n[Fact]\npublic void Add_WhenInputsProvided_ReturnsSum()\n{\n}\n```"
        ]);
        var service = CreateService(provider);
        var request = CreateRequest(
            metricsPath: MetricsDrivenPath.Coverage,
            mutationSummary: "Mutation evidence to target:\n- Mutant 1: Survived, EqualityOperator, lines 3-3, replacement=`>=`",
            steps: PlanningDisabledExceptScenarioAndMethodName());

        var result = await service.GenerateTestAsync(request);

        Assert.True(result.Success);
        Assert.DoesNotContain("Mutation context:", provider.Prompts[0]);
        Assert.DoesNotContain("EqualityOperator", provider.Prompts[0]);
    }

    private static TestGenerationPipelineService CreateService(RecordingProvider provider)
    {
        var config = new TestMapConfig();
        config.AiProviderConfig.OpenAi.ApiKey = "test";
        return new TestGenerationPipelineService(
            new ProjectContext(new ProjectModel(config: config)),
            config,
            [provider]);
    }

    private static TestGenerationRequest CreateRequest(
        GenerationContextMode contextMode = GenerationContextMode.ChainedHistory,
        MetricsDrivenPath? metricsPath = null,
        string mutationSummary = "",
        GenerationStepConfig? steps = null)
    {
        return new TestGenerationRequest
        {
            MethodBody = "public int Add(int x, int y) => x + y;",
            MethodName = "Add",
            MethodSignature = "public int Add(int x, int y)",
            ContainingClass = "public class Calculator { }",
            ExampleTest = "[Fact] public void Existing() { }",
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = "public class CalculatorTests { }",
            TestFileContents = "public class CalculatorTests { }",
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = "using Xunit;",
            MetricsPath = metricsPath,
            CoverageGapSummary = string.Empty,
            MutationSummary = mutationSummary,
            Provider = AiProvider.OpenAi,
            ContextMode = contextMode,
            Steps = steps ?? new GenerationStepConfig()
        };
    }

    private static GenerationStepConfig PlanningDisabledExceptScenarioAndMethodName()
    {
        return new GenerationStepConfig
        {
            EnableScenario = true,
            EnableMethodName = true,
            EnableArrangePlan = false,
            EnableInputPlan = false,
            EnableActionPlan = false,
            EnableAssertionPlan = false
        };
    }

    private sealed class RecordingProvider : IAiGenerationProvider
    {
        private readonly Queue<string> _responses;

        public RecordingProvider(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public AiProvider Provider => AiProvider.OpenAi;
        public List<string> Prompts { get; } = [];

        public Task CreateAsync(
            IAiProviderConfig providerConfig,
            AiProviderMode mode,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> GenerateAsync(
            string prompt,
            double temperature = 0,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            return Task.FromResult(_responses.Count == 0 ? string.Empty : _responses.Dequeue());
        }
    }
}
