using TestMap.App;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.UnitTests.TestGeneration;

/// <summary>
/// Tests for the repair path in <see cref="TestGenerationPipelineService"/>,
/// specifically the Basic Extension repair branch — covering application failures,
/// build failures, and runtime/assertion failures.
/// </summary>
public sealed class TestGenerationPipelineServiceRepairTests
{
    // ---------------------------------------------------------------------------
    // Repair prompt branching
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RepairTestAsync_BasicExtensionWithBuildFailure_UsesPostPatchFileInPrompt()
    {
        // When UseStructuredPatchOutput is true and ModifiedTestFileContents is set (build failure),
        // the post-patch integrated file content must appear in the prompt so the model
        // sees the state that failed to compile.
        const string originalFile = "// original test file content";
        const string modifiedFile = "// post-patch test file content — has the new test";

        var provider = new RecordingProvider(["```json\n{\"testMethod\":\"[Fact]\\npublic void T(){}\",\"testMethodName\":\"T\"}\n```"]);
        var service = CreateService(provider);
        var request = CreateRepairRequest(
            useStructuredPatchOutput: true,
            testFileContents: originalFile,
            modifiedTestFileContents: modifiedFile,
            structuredErrors: "CS0103 error");

        await service.RepairTestAsync(request);

        Assert.Single(provider.Prompts);
        Assert.Contains(modifiedFile, provider.Prompts[0]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RepairTestAsync_BasicExtensionWithBuildFailure_ShowsBothOriginalAndPostPatchFiles()
    {
        // When UseStructuredPatchOutput is true and ModifiedTestFileContents is set,
        // the original file appears under "Original test file" and the post-patch file
        // appears under "Post-patch test file". Both must be in the prompt so the model
        // understands what changed and why compilation failed.
        const string originalFile = "// ORIGINAL_MARKER";
        const string modifiedFile = "// MODIFIED_MARKER";

        var provider = new RecordingProvider(["```json\n{\"testMethod\":\"[Fact]\\npublic void T(){}\",\"testMethodName\":\"T\"}\n```"]);
        var service = CreateService(provider);
        var request = CreateRepairRequest(
            useStructuredPatchOutput: true,
            testFileContents: originalFile,
            modifiedTestFileContents: modifiedFile,
            structuredErrors: "CS0103 error");

        await service.RepairTestAsync(request);

        var prompt = provider.Prompts[0];
        Assert.Contains("ORIGINAL_MARKER", prompt);
        Assert.Contains("MODIFIED_MARKER", prompt);
        Assert.Contains("Post-patch test file", prompt);
        Assert.Contains("Original test file", prompt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RepairTestAsync_BasicExtension_AlwaysRequestsStructuredPatchJsonOutput()
    {
        // When UseStructuredPatchOutput is true, ALL repair attempts (application failure,
        // build failure, runtime failure) must request a BasicExtensionPatch JSON object,
        // not raw C# code. ModifiedTestFileContents may or may not be present.
        var provider = new RecordingProvider(["```json\n{\"testMethod\":\"[Fact]\\npublic void T(){}\",\"testMethodName\":\"T\"}\n```"]);
        var service = CreateService(provider);

        // Application/runtime failure: no post-patch snapshot.
        var requestNoSnapshot = CreateRepairRequest(
            useStructuredPatchOutput: true,
            modifiedTestFileContents: null);

        await service.RepairTestAsync(requestNoSnapshot);

        var prompt = provider.Prompts[0];
        Assert.Contains("usingsToAdd", prompt);
        Assert.Contains("helperMethodsToAdd", prompt);
        Assert.Contains("testMethod", prompt);
        Assert.Contains("testMethodName", prompt);
        Assert.Contains("```json", prompt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RepairTestAsync_NonBasicExtension_UsesLegacyRepairPrompt()
    {
        // When UseStructuredPatchOutput is false (non-BasicExtension approaches),
        // the legacy compile-repair or behavior-repair prompt is used regardless of
        // whether ModifiedTestFileContents is set. The prompt requests raw C# method output.
        var provider = new RecordingProvider(["```csharp\n[Fact] public void T() { }\n```"]);
        var service = CreateService(provider);
        var request = CreateRepairRequest(
            useStructuredPatchOutput: false,
            testFileContents: "// original file",
            modifiedTestFileContents: null,
            structuredErrors: "CS0103 error");

        await service.RepairTestAsync(request);

        var prompt = provider.Prompts[0];
        // Legacy prompt asks for C# code, not a JSON schema.
        Assert.DoesNotContain("\"usingsToAdd\"", prompt);
        Assert.DoesNotContain("```json", prompt);
        Assert.Contains("```", prompt); // requests code block
    }

    // ---------------------------------------------------------------------------
    // Method name extraction from patch JSON
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RepairTestAsync_ValidPatchJsonResponse_ExtractsTestMethodNameFromPatch()
    {
        /// <summary>
        /// When the repair response is a BasicExtensionPatch JSON and Roslyn method-name
        /// extraction fails (because the response is JSON, not raw C#), the pipeline
        /// falls back to deserializing the patch and returning the testMethodName field.
        /// </summary>
        const string patchJson = """
            ```json
            {
              "usingsToAdd": [],
              "helperMethodsToAdd": [],
              "testMethod": "[Fact]\npublic void Calculate_ReturnsZero()\n{\n    Assert.Equal(0, sut.Calculate());\n}",
              "testMethodName": "Calculate_ReturnsZero",
              "integrationRationale": "Fixed missing assertion import."
            }
            ```
            """;

        var provider = new RecordingProvider([patchJson]);
        var service = CreateService(provider);
        var request = CreateRepairRequest(
            useStructuredPatchOutput: true,
            modifiedTestFileContents: "// post-patch content",
            structuredErrors: "CS0103 error");

        var result = await service.RepairTestAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Calculate_ReturnsZero", result.TestMethodName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RepairTestAsync_PatchJsonWithNoTestMethodName_ReturnsNullMethodName()
    {
        /// <summary>
        /// When the patch JSON has an empty testMethodName and the response is not
        /// valid raw C#, TestMethodName in the result is null. This is safe because
        /// the executor will extract the method name from the parsed patch at apply time.
        /// </summary>
        const string patchJson = """
            ```json
            {
              "testMethod": "[Fact]\npublic void Add_ReturnsSum()\n{\n    Assert.Equal(3, 1 + 2);\n}",
              "testMethodName": ""
            }
            ```
            """;

        var provider = new RecordingProvider([patchJson]);
        var service = CreateService(provider);
        var request = CreateRepairRequest(
            useStructuredPatchOutput: true,
            modifiedTestFileContents: "// post-patch content",
            structuredErrors: "CS0103 error");

        var result = await service.RepairTestAsync(request);

        Assert.True(result.Success);
        Assert.Null(result.TestMethodName);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TestGenerationPipelineService CreateService(RecordingProvider provider)
    {
        var config = new TestMapConfig();
        config.AiProviderConfig.OpenAi.ApiKey = "test";
        return new TestGenerationPipelineService(
            new ProjectContext(new ProjectModel(config: config)),
            config,
            [provider]);
    }

    private static TestRepairRequest CreateRepairRequest(
        bool useStructuredPatchOutput = false,
        string? testFileContents = null,
        string? modifiedTestFileContents = null,
        string? structuredErrors = null,
        string? errorLogs = null)
    {
        return new TestRepairRequest
        {
            MethodBody = "public int Calculate() => 0;",
            MethodName = "Calculate",
            GeneratedTest = "[Fact] public void Calculate_Returns0() { Assert.Equal(0, sut.Calculate()); }",
            TestClass = "public sealed class CalculatorTests { }",
            TestFramework = "xUnit",
            TestDependencies = "using Xunit;",
            TestFileContents = testFileContents ?? "public sealed class CalculatorTests { }",
            TestSupportContext = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            CoverageGapSummary = string.Empty,
            ErrorLogs = errorLogs ?? "Build failed: 1 error(s).",
            StructuredErrors = structuredErrors,
            ModifiedTestFileContents = modifiedTestFileContents,
            UseStructuredPatchOutput = useStructuredPatchOutput,
            Provider = AiProvider.OpenAi
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
            CancellationToken cancellationToken = default) => Task.CompletedTask;

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
