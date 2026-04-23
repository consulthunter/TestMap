using System.Diagnostics;
using System.Text.Json;
using SharpToken;
using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;
using TestMap.Models.Generation;
using TestMap.Services.Testing.Providers.Abstractions;

namespace TestMap.Services.Testing;

/// <summary>
/// Shared implementation of decomposed test generation pipeline.
/// Used by both regular test generation and AI provider comparison experiments.
/// </summary>
public class TestGenerationPipelineService : ITestGenerationPipelineService
{
    private const string PromptVersion = "generation-pipeline-v2";
    private readonly ProjectContext _context;
    private readonly TestMapConfig _config;
    private readonly Dictionary<AiProvider, IAiGenerationProvider> _providers;
    private readonly GptEncoding _encoding;

    public TestGenerationPipelineService(
        ProjectContext context,
        TestMapConfig config,
        IEnumerable<IAiGenerationProvider> providers)
    {
        _context = context;
        _config = config;
        _providers = providers.ToDictionary(x => x.Provider);
        _encoding = GptEncoding.GetEncoding("cl100k_base");
    }

    public async Task<TestGenerationResult> GenerateTestAsync(
        TestGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderAsync(request.Provider, cancellationToken);
        var steps = new List<GenerationStepMetadata>();
        var conversation = GenerationConversationState.Create(request.EnableHistoryChaining);
        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Generate Scenario
            var scenarioPrompt = CreateScenarioPrompt(request);
            var scenario = await ExecuteStepAsync(
                GenerationStepType.Scenario,
                PreparePrompt(
                    conversation,
                    GenerationStepType.Scenario,
                    scenarioPrompt),
                provider,
                request.Temperature,
                request.StepErrorRetries,
                request.StepRetryDelayMs,
                cancellationToken);
            steps.Add(scenario);
            if (!scenario.Success) return CreateFailureResult(steps, overallStopwatch, scenario.ErrorMessage);
            conversation.Append(GenerationStepType.Scenario, scenarioPrompt, scenario.Response);

            // Step 2: Generate Method Name
            var methodNamePrompt = CreateMethodNamePrompt(request, scenario.Response);
            var methodName = await ExecuteStepAsync(
                GenerationStepType.MethodName,
                PreparePrompt(
                    conversation,
                    GenerationStepType.MethodName,
                    methodNamePrompt),
                provider,
                request.Temperature,
                request.StepErrorRetries,
                request.StepRetryDelayMs,
                cancellationToken);
            steps.Add(methodName);
            if (!methodName.Success) return CreateFailureResult(steps, overallStopwatch, methodName.ErrorMessage);
            conversation.Append(GenerationStepType.MethodName, methodNamePrompt, methodName.Response);

            // Step 3: Generate Arrange Plan
            var arrangePrompt = CreateArrangePlanPrompt(request, scenario.Response);
            var arrangePlan = await ExecuteStructuredStepAsync<ArrangePlan>(
                GenerationStepType.ArrangePlan,
                PreparePrompt(
                    conversation,
                    GenerationStepType.ArrangePlan,
                    arrangePrompt),
                provider,
                request.Temperature,
                request.StepErrorRetries,
                request.StepRetryDelayMs,
                cancellationToken);
            steps.Add(arrangePlan.Metadata);
            if (!arrangePlan.Metadata.Success) return CreateFailureResult(steps, overallStopwatch, arrangePlan.Metadata.ErrorMessage);
            conversation.Append(GenerationStepType.ArrangePlan, arrangePrompt, arrangePlan.Metadata.Response);

            // Step 4: Generate Input Plan
            var inputPrompt = CreateInputPlanPrompt(request, scenario.Response, arrangePlan.Structured);
            var inputPlan = await ExecuteStructuredStepAsync<InputPlan>(
                GenerationStepType.InputPlan,
                PreparePrompt(
                    conversation,
                    GenerationStepType.InputPlan,
                    inputPrompt),
                provider,
                request.Temperature,
                request.StepErrorRetries,
                request.StepRetryDelayMs,
                cancellationToken);
            steps.Add(inputPlan.Metadata);
            if (!inputPlan.Metadata.Success) return CreateFailureResult(steps, overallStopwatch, inputPlan.Metadata.ErrorMessage);
            conversation.Append(GenerationStepType.InputPlan, inputPrompt, inputPlan.Metadata.Response);

            // Step 5: Generate Action Plan
            var actionPrompt = CreateActionPlanPrompt(request, scenario.Response, inputPlan.Structured);
            var actionPlan = await ExecuteStructuredStepAsync<ActionPlan>(
                GenerationStepType.ActionPlan,
                PreparePrompt(
                    conversation,
                    GenerationStepType.ActionPlan,
                    actionPrompt),
                provider,
                request.Temperature,
                request.StepErrorRetries,
                request.StepRetryDelayMs,
                cancellationToken);
            steps.Add(actionPlan.Metadata);
            if (!actionPlan.Metadata.Success) return CreateFailureResult(steps, overallStopwatch, actionPlan.Metadata.ErrorMessage);
            conversation.Append(GenerationStepType.ActionPlan, actionPrompt, actionPlan.Metadata.Response);

            // Step 6: Generate Assertion Plan
            var assertionPrompt = CreateAssertionPlanPrompt(request, scenario.Response, actionPlan.Structured);
            var assertionPlan = await ExecuteStructuredStepAsync<AssertionPlan>(
                GenerationStepType.AssertionPlan,
                PreparePrompt(
                    conversation,
                    GenerationStepType.AssertionPlan,
                    assertionPrompt),
                provider,
                request.Temperature,
                request.StepErrorRetries,
                request.StepRetryDelayMs,
                cancellationToken);
            steps.Add(assertionPlan.Metadata);
            if (!assertionPlan.Metadata.Success) return CreateFailureResult(steps, overallStopwatch, assertionPlan.Metadata.ErrorMessage);
            conversation.Append(GenerationStepType.AssertionPlan, assertionPrompt, assertionPlan.Metadata.Response);

            // Step 7: Generate Final Test
            var finalTestPrompt = CreateFinalTestPrompt(
                request,
                scenario.Response,
                methodName.Response,
                arrangePlan,
                inputPlan,
                actionPlan,
                assertionPlan);
            var finalTestStep = await ExecuteStepAsync(
                GenerationStepType.FinalTest,
                PreparePrompt(
                    conversation,
                    GenerationStepType.FinalTest,
                    finalTestPrompt),
                provider,
                request.Temperature,
                request.StepErrorRetries,
                request.StepRetryDelayMs,
                cancellationToken);
            steps.Add(finalTestStep);
            if (!finalTestStep.Success) return CreateFailureResult(steps, overallStopwatch, finalTestStep.ErrorMessage);
            conversation.Append(GenerationStepType.FinalTest, finalTestPrompt, finalTestStep.Response);

            overallStopwatch.Stop();

            var finalTest = ExtractCodeBlock(finalTestStep.Response);
            var extractedMethodName = Utilities.Utilities.ExtractTestMethodName(finalTest) ?? ExtractMethodName(methodName.Response);

            return new TestGenerationResult
            {
                Success = true,
                GeneratedTest = finalTest,
                TestMethodName = extractedMethodName,
                Steps = steps,
                TotalDurationSeconds = overallStopwatch.Elapsed.TotalSeconds,
                TotalTokens = steps.Sum(s => s.TokenCount),
                ConversationTranscript = conversation.Export()
            };
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error($"Test generation pipeline failed: {ex.Message}");
            overallStopwatch.Stop();
            return CreateFailureResult(steps, overallStopwatch, ex.Message);
        }
    }

    public async Task<TestGenerationResult> RepairTestAsync(
        TestRepairRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderAsync(request.Provider, cancellationToken);
        var steps = new List<GenerationStepMetadata>();
        var conversation = GenerationConversationState.Create(
            request.EnableHistoryChaining,
            request.PriorConversationTranscript);
        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            var repairStepType = DetermineRepairStepType(request);
            var rawRepairPrompt = CreateRepairPrompt(request);
            var repairPrompt = PreparePrompt(
                conversation,
                repairStepType,
                rawRepairPrompt);
            var result = await ExecuteStepAsync(
                repairStepType,
                repairPrompt,
                provider,
                request.Temperature,
                request.StepErrorRetries,
                request.StepRetryDelayMs,
                cancellationToken);

            steps.Add(result);
            overallStopwatch.Stop();
            if (result.Success)
            {
                conversation.Append(repairStepType, rawRepairPrompt, result.Response);
            }

            if (!result.Success)
            {
                return CreateFailureResult(steps, overallStopwatch, result.ErrorMessage);
            }

            var repairedTest = ExtractCodeBlock(result.Response);
            var testMethodName = Utilities.Utilities.ExtractTestMethodName(repairedTest);

            return new TestGenerationResult
            {
                Success = true,
                GeneratedTest = repairedTest,
                TestMethodName = testMethodName,
                Steps = steps,
                TotalDurationSeconds = overallStopwatch.Elapsed.TotalSeconds,
                TotalTokens = steps.Sum(s => s.TokenCount),
                ConversationTranscript = conversation.Export()
            };
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error($"Test repair failed: {ex.Message}");
            overallStopwatch.Stop();
            return CreateFailureResult(steps, overallStopwatch, ex.Message);
        }
    }

    private async Task<IAiGenerationProvider> GetProviderAsync(
        AiProvider providerType,
        CancellationToken cancellationToken)
    {
        if (!_providers.TryGetValue(providerType, out var provider))
        {
            throw new InvalidOperationException($"Provider {providerType} not registered.");
        }

        var providerConfig = _config.AiProviderConfig.GetProviderConfig(providerType)
            ?? throw new InvalidOperationException($"Provider config not found for {providerType}.");

        var generationConfig = _config.TestingConfig.GenerationConfig;
        await provider.CreateAsync(providerConfig, generationConfig.Mode, cancellationToken);

        return provider;
    }

    private async Task<GenerationStepMetadata> ExecuteStepAsync(
        GenerationStepType stepType,
        string prompt,
        IAiGenerationProvider provider,
        double temperature,
        int stepErrorRetries,
        int stepRetryDelayMs,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, stepErrorRetries + 1);
        var tokenCount = _encoding.Encode(prompt).Count;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (attempt == 1)
                {
                    _context.Project.Logger?.Information($"Executing step: {stepType}");
                }
                else
                {
                    _context.Project.Logger?.Warning(
                        "Retrying step {StepType} ({Attempt}/{MaxAttempts})",
                        stepType,
                        attempt,
                        maxAttempts);
                }

                var response = await provider.GenerateAsync(prompt, temperature, cancellationToken);
                stopwatch.Stop();

                _context.Project.Logger?.Information($"Step {stepType} completed in {stopwatch.Elapsed.TotalSeconds:F2}s, {tokenCount} tokens");

                return new GenerationStepMetadata
                {
                    StepType = stepType,
                    Prompt = prompt,
                    Response = response,
                    ResponseFormat = GetResponseFormat(stepType),
                    PromptVersion = PromptVersion,
                    ValidationStatus = GetValidationStatus(stepType, !string.IsNullOrWhiteSpace(response)),
                    TokenCount = tokenCount,
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                    StartedAt = startedAt,
                    CompletedAt = DateTime.UtcNow,
                    Success = !string.IsNullOrWhiteSpace(response)
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                if (attempt < maxAttempts)
                {
                    _context.Project.Logger?.Warning(
                        "Step {StepType} failed on attempt {Attempt}/{MaxAttempts}: {Message}",
                        stepType,
                        attempt,
                        maxAttempts,
                        ex.Message);

                    if (stepRetryDelayMs > 0)
                    {
                        await Task.Delay(stepRetryDelayMs, cancellationToken);
                    }

                    continue;
                }

                _context.Project.Logger?.Error($"Step {stepType} failed: {ex.Message}");

                return new GenerationStepMetadata
                {
                    StepType = stepType,
                    Prompt = prompt,
                    Response = string.Empty,
                    ResponseFormat = GetResponseFormat(stepType),
                    PromptVersion = PromptVersion,
                    ValidationStatus = "failed",
                    TokenCount = tokenCount,
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                    StartedAt = startedAt,
                    CompletedAt = DateTime.UtcNow,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        return new GenerationStepMetadata
        {
            StepType = stepType,
            Prompt = prompt,
            Response = string.Empty,
            ResponseFormat = GetResponseFormat(stepType),
            PromptVersion = PromptVersion,
            ValidationStatus = "failed",
            TokenCount = tokenCount,
            DurationSeconds = 0,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Success = false,
            ErrorMessage = "Unknown step failure"
        };
    }

    #region Prompt Generation

    private async Task<StructuredStepResult<TPlan>> ExecuteStructuredStepAsync<TPlan>(
        GenerationStepType stepType,
        string prompt,
        IAiGenerationProvider provider,
        double temperature,
        int stepErrorRetries,
        int stepRetryDelayMs,
        CancellationToken cancellationToken)
        where TPlan : class, new()
    {
        var metadata = await ExecuteStepAsync(
            stepType,
            prompt,
            provider,
            temperature,
            stepErrorRetries,
            stepRetryDelayMs,
            cancellationToken);

        var (json, parsed) = TryParseStructuredResponse<TPlan>(metadata.Response);
        var enriched = new GenerationStepMetadata
        {
            StepType = metadata.StepType,
            Prompt = metadata.Prompt,
            Response = metadata.Response,
            ResponseFormat = "application/json",
            StructuredResponseJson = json,
            PromptVersion = PromptVersion,
            ValidationStatus = parsed != null ? "valid_json" : "invalid_json",
            TokenCount = metadata.TokenCount,
            DurationSeconds = metadata.DurationSeconds,
            StartedAt = metadata.StartedAt,
            CompletedAt = metadata.CompletedAt,
            Success = metadata.Success && parsed != null,
            ErrorMessage = parsed != null ? metadata.ErrorMessage : "Structured response could not be parsed."
        };

        return new StructuredStepResult<TPlan>(enriched, parsed ?? new TPlan());
    }

    private static (string? Json, TPlan? Parsed) TryParseStructuredResponse<TPlan>(string response)
        where TPlan : class
    {
        var json = ExtractJson(response);
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, null);
        }

        try
        {
            return (json, JsonSerializer.Deserialize<TPlan>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }));
        }
        catch
        {
            return (json, null);
        }
    }

    private static GenerationStepType DetermineRepairStepType(TestRepairRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.StructuredErrors)
            ? GenerationStepType.CompileRepair
            : GenerationStepType.BehaviorRepair;
    }

    private static string PreparePrompt(
        GenerationConversationState conversation,
        GenerationStepType stepType,
        string prompt)
    {
        if (!conversation.Enabled)
        {
            return prompt;
        }

        var transcript = conversation.Export();
        var transcriptBlock = string.IsNullOrWhiteSpace(transcript)
            ? "No prior conversation."
            : transcript;

        return $@"You are continuing a multi-step conversation to design a single C# test.
Stay consistent with previously accepted decisions unless this instruction explicitly revises them.

Conversation so far:
{transcriptBlock}

Current step: {stepType}
Instruction:
{prompt}";
    }

    private string CreateScenarioPrompt(TestGenerationRequest request)
    {
        return $@"Given this method:
{request.MethodBody}

And this example test from the same test class:
{request.ExampleTest}

Full test file for local context:
{request.TestFileContents}

Relevant helper/setup members already present in that file:
{request.TestSupportContext}

Example test metadata:
{request.ExampleTestMetadataSummary}

Project test metadata patterns:
{request.ProjectTestMetadataSummary}

Describe a specific test scenario that would extend coverage of the target method.
Focus on an edge case or untested path.
Use this coverage gap information when deciding which path to target:
{request.CoverageGapSummary}

Respond with only the scenario description, 1-2 sentences.";
    }

    private string CreateMethodNamePrompt(TestGenerationRequest request, string scenario)
    {
        return $@"Given this test scenario:
{scenario}

For this method:
{request.MethodSignature}

Generate an appropriate test method name following {request.TestFramework} conventions.
The name should clearly describe what is being tested.
Respond with only the method name, nothing else.";
    }

    private string CreateArrangePlanPrompt(TestGenerationRequest request, string scenario)
    {
        return $@"For this test scenario:
{scenario}

And this method under test:
{request.MethodBody}

Produce strict JSON for the arrange plan in this shape:
{{
  ""dependencies"": [
    {{ ""name"": ""sut"", ""kind"": ""system_under_test"", ""construction"": ""..."", ""isMock"": false }}
  ],
  ""requiredNamespaces"": [""Namespace.Name""],
  ""helperReuse"": [""ExistingBuilderOrFixture""],
  ""requiresMocks"": true,
  ""arrangeStrategy"": ""short explanation""
}}

Decide:
- what objects must exist
- how they should be constructed
- whether mocks are needed
- whether existing helpers should be reused
- what namespaces/framework symbols are required

Relevant coverage gaps:
{request.CoverageGapSummary}

Respond with only JSON.";
    }

    private string CreateInputPlanPrompt(TestGenerationRequest request, string scenario, ArrangePlan arrangePlan)
    {
        return $@"For this test scenario:
{scenario}

Method under test:
{request.MethodBody}

Arrange plan:
{JsonSerializer.Serialize(arrangePlan)}

Return strict JSON in this shape:
{{
  ""inputs"": [""var id = 1;""],
  ""preconditions"": [""seed repository with ...""]
}}

Relevant coverage gaps:
{request.CoverageGapSummary}

Respond with only JSON.";
    }

    private string CreateActionPlanPrompt(TestGenerationRequest request, string scenario, InputPlan inputPlan)
    {
        return $@"For this test scenario:
{scenario}

Method under test:
{request.MethodBody}

Input plan:
{JsonSerializer.Serialize(inputPlan)}

Return strict JSON in this shape:
{{
  ""invocation"": ""var result = sut.DoThing(input);"",
  ""resultBinding"": ""result""
}}

Respond with only JSON.";
    }

    private string CreateAssertionPlanPrompt(TestGenerationRequest request, string scenario, ActionPlan actionPlan)
    {
        return $@"For this test scenario:
{scenario}

Action plan:
{JsonSerializer.Serialize(actionPlan)}

Generate appropriate assertions using {request.TestFramework} assertion syntax.
The assertions should verify the expected behavior described in the scenario.

Example test for reference:
{request.ExampleTest}

Example test metadata:
{request.ExampleTestMetadataSummary}

Relevant coverage gaps:
{request.CoverageGapSummary}

Return strict JSON in this shape:
{{
  ""assertions"": [""Assert.Equal(expected, result);""],
  ""expectedBehavior"": ""short explanation""
}}

Respond with only JSON.";
    }

    private string CreateFinalTestPrompt(
        TestGenerationRequest request,
        string scenario,
        string methodName,
        StructuredStepResult<ArrangePlan> arrangePlan,
        StructuredStepResult<InputPlan> inputPlan,
        StructuredStepResult<ActionPlan> actionPlan,
        StructuredStepResult<AssertionPlan> assertionPlan)
    {
        return $@"Write the complete final {request.TestFramework} test method for this target method:
{request.MethodBody}

Scenario:
{scenario}

Test method name:
{methodName}

Arrange plan:
{arrangePlan.Metadata.StructuredResponseJson ?? arrangePlan.Metadata.Response}

Input plan:
{inputPlan.Metadata.StructuredResponseJson ?? inputPlan.Metadata.Response}

Action plan:
{actionPlan.Metadata.StructuredResponseJson ?? actionPlan.Metadata.Response}

Assertion plan:
{assertionPlan.Metadata.StructuredResponseJson ?? assertionPlan.Metadata.Response}

Use this example test from the same class for style and framework conventions:
{request.ExampleTest}

Example test metadata:
{request.ExampleTestMetadataSummary}

Project-wide test metadata patterns:
{request.ProjectTestMetadataSummary}

The generated test will be inserted into this test class:
{request.TestClass}

The full current test file is:
{request.TestFileContents}

Reusable helper/setup members already available:
{request.TestSupportContext}

Available dependencies:
{request.TestDependencies}

Coverage gaps to target:
{request.CoverageGapSummary}

Requirements:
- Return a complete test method only, not a scaffold or outline
- Use the requested method name
- Include an appropriate test attribute for {request.TestFramework}
- Include an XML doc comment describing the scenario
- Include // Arrange, // Act, // Assert comments
- Ensure the code is valid C# and internally consistent
- Do not include any explanation outside the code block

Respond with only the complete test method code wrapped in ```csharp.";
    }

    private string CreateRepairPrompt(TestRepairRequest request)
    {
        if (DetermineRepairStepType(request) == GenerationStepType.CompileRepair)
        {
            return $@"I previously attempted to generate a test for this method:
{request.MethodBody}

The generated test was:
{request.GeneratedTest}

This test is part of the class:
{request.TestClass}

The full current test file is:
{request.TestFileContents}

Reusable helper/setup members already available:
{request.TestSupportContext}

The test class dependencies are:
{request.TestDependencies}

Example test metadata:
{request.ExampleTestMetadataSummary}

Project-wide test metadata patterns:
{request.ProjectTestMetadataSummary}

Coverage gaps for the target method are:
{request.CoverageGapSummary}

The test run (attempt #{request.AttemptNumber}) produced these errors:
{request.ErrorLogs}

Structured compiler/build diagnostics:
{request.StructuredErrors ?? "No structured diagnostics available."}

Focus only on compile repair. Fix:
- missing usings
- wrong framework attributes/assertions
- invalid constructors/setup
- unresolved symbols
- malformed method structure

Respond with only the complete test method code wrapped in ```.";
        }

        return $@"I previously attempted to generate a test for this method:
{request.MethodBody}

The generated test was:
{request.GeneratedTest}

The full current test file is:
{request.TestFileContents}

Reusable helper/setup members already available:
{request.TestSupportContext}

The build succeeded, but behavior or coverage was insufficient.

Observed execution/test feedback:
{request.ErrorLogs}

Coverage gaps for the target method are:
{request.CoverageGapSummary}

Focus only on behavior repair. Fix:
- wrong assertion logic
- brittle expectations
- weak input selection
- poor coverage targeting

Respond with only the complete test method code wrapped in ```.";
    }

    #endregion

    #region Helper Methods

    private string ExtractCodeBlock(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        if (response.Contains("```"))
        {
            var parts = response.Split("```");
            if (parts.Length >= 2)
            {
                return parts[1].Replace("csharp", "").Trim();
            }
        }

        return response.Trim();
    }

    private string ExtractMethodName(string response)
    {
        var cleaned = response.Trim();

        if (cleaned.StartsWith("Test_") || cleaned.StartsWith("test_") ||
            cleaned.Contains("Should") || cleaned.Contains("_"))
        {
            return cleaned;
        }

        return cleaned;
    }

    private static string ExtractJson(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        var trimmed = response.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var parts = trimmed.Split("```", StringSplitOptions.RemoveEmptyEntries);
            var candidate = parts.FirstOrDefault(x => x.Contains('{'));
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                trimmed = candidate.Replace("json", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            }
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : string.Empty;
    }

    private static string GetResponseFormat(GenerationStepType stepType)
    {
        return stepType switch
        {
            GenerationStepType.FinalTest => "text/x-csharp",
            GenerationStepType.CompileRepair => "text/x-csharp",
            GenerationStepType.BehaviorRepair => "text/x-csharp",
            _ => "text/plain"
        };
    }

    private static string GetValidationStatus(GenerationStepType stepType, bool hasResponse)
    {
        if (!hasResponse)
        {
            return "empty";
        }

        return stepType switch
        {
            GenerationStepType.FinalTest => "code_generated",
            GenerationStepType.CompileRepair => "code_generated",
            GenerationStepType.BehaviorRepair => "code_generated",
            _ => "raw"
        };
    }

    private TestGenerationResult CreateFailureResult(
        List<GenerationStepMetadata> steps,
        Stopwatch stopwatch,
        string? errorMessage)
    {
        stopwatch.Stop();
        return new TestGenerationResult
        {
            Success = false,
            Steps = steps,
            TotalDurationSeconds = stopwatch.Elapsed.TotalSeconds,
            TotalTokens = steps.Sum(s => s.TokenCount),
            ErrorMessage = errorMessage
        };
    }

    private sealed class GenerationConversationState
    {
        private readonly List<string> _entries = new();

        private GenerationConversationState(bool enabled)
        {
            Enabled = enabled;
        }

        public bool Enabled { get; }

        public static GenerationConversationState Create(bool enabled, string? existingTranscript = null)
        {
            var state = new GenerationConversationState(enabled);
            if (enabled && !string.IsNullOrWhiteSpace(existingTranscript))
            {
                state._entries.Add(existingTranscript.Trim());
            }

            return state;
        }

        public void Append(GenerationStepType stepType, string prompt, string response)
        {
            if (!Enabled)
            {
                return;
            }

            _entries.Add($@"[{stepType}] User:
{prompt}

[{stepType}] Assistant:
{response}");
        }

        public string? Export()
        {
            if (!Enabled || _entries.Count == 0)
            {
                return null;
            }

            return string.Join("\n\n", _entries);
        }
    }

    private sealed record StructuredStepResult<TPlan>(GenerationStepMetadata Metadata, TPlan Structured);

    #endregion
}
