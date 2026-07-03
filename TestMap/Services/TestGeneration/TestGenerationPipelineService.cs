using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpToken;
using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Generation;
using TestMap.Services.TestGeneration.Context;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.TestGeneration;

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
    private readonly IContextGraphService _contextGraphService;
    private readonly IContextResolutionService _contextResolutionService;
    private readonly GptEncoding _encoding;

    public TestGenerationPipelineService(
        ProjectContext context,
        TestMapConfig config,
        IEnumerable<IAiGenerationProvider> providers,
        IContextGraphService? contextGraphService = null,
        IContextResolutionService? contextResolutionService = null)
    {
        _context = context;
        _config = config;
        _providers = providers.ToDictionary(x => x.Provider);
        _contextGraphService = contextGraphService ?? new ContextGraphService();
        _contextResolutionService = contextResolutionService ?? new ContextResolutionService();
        _encoding = GptEncoding.GetEncoding("cl100k_base");
    }

    public async Task<TestGenerationResult> GenerateTestAsync(
        TestGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderAsync(request.Provider, cancellationToken);
        var steps = new List<GenerationStepMetadata>();
        var conversation = GenerationConversationState.Create(ShouldUseConversationHistory(request));
        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            var promptEvidence = GenerationPromptEvidence.FromRequest(request);
            var artifacts = new GenerationPipelineArtifacts();

            if (request.Steps.EnableContextGraph)
            {
                artifacts.ContextGraph = await _contextGraphService.BuildAsync(request, cancellationToken);
                steps.Add(CreateContextGraphStep(artifacts.ContextGraph));
            }

            if (request.Steps.EnableContextResolution)
            {
                if (artifacts.ContextGraph == null)
                {
                    artifacts.ContextGraph = await _contextGraphService.BuildAsync(request, cancellationToken);
                    steps.Add(CreateContextGraphStep(artifacts.ContextGraph));
                }

                artifacts.ContextResolution = _contextResolutionService.Resolve(artifacts.ContextGraph);
                steps.Add(CreateContextResolutionStep(artifacts.ContextResolution));
            }

            var contextSummary = BuildContextSummary(artifacts);

            if (ShouldUseBasicExtensionOneShot(request))
            {
                return await GenerateBasicExtensionOneShotAsync(
                    request,
                    promptEvidence,
                    contextSummary,
                    steps,
                    conversation,
                    provider,
                    overallStopwatch,
                    cancellationToken);
            }

            // Step 1: Generate Scenario
            var scenario = request.Steps.EnableScenario
                ? await ExecuteStepAsync(
                    GenerationStepType.Scenario,
                    PreparePrompt(
                        conversation,
                        request.ContextMode,
                        GenerationStepType.Scenario,
                        CreateScenarioPrompt(promptEvidence)),
                    provider,
                    request.Temperature,
                    request.StepErrorRetries,
                    request.StepRetryDelayMs,
                    cancellationToken)
                : CreateFallbackStep(
                    GenerationStepType.Scenario,
                    BuildFallbackScenario(promptEvidence),
                    "Scenario step disabled; final synthesis will use target evidence directly.");
            steps.Add(scenario);
            if (!scenario.Success) return CreateFailureResult(steps, overallStopwatch, scenario.ErrorMessage);
            artifacts.Scenario = scenario.Response;
            if (scenario.Status == GenerationStepStatus.Executed)
                conversation.Append(GenerationStepType.Scenario, CreateScenarioPrompt(promptEvidence), scenario.Response);

            // Step 2: Generate Method Name
            var methodName = request.Steps.EnableMethodName
                ? await ExecuteStepAsync(
                    GenerationStepType.MethodName,
                    PreparePrompt(
                        conversation,
                        request.ContextMode,
                        GenerationStepType.MethodName,
                        CreateMethodNamePrompt(promptEvidence, artifacts.Scenario ?? string.Empty)),
                    provider,
                    request.Temperature,
                    request.StepErrorRetries,
                    request.StepRetryDelayMs,
                    cancellationToken)
                : CreateFallbackStep(
                    GenerationStepType.MethodName,
                    BuildFallbackMethodName(promptEvidence),
                    "Method-name step disabled; deterministic fallback method name was used.");
            steps.Add(methodName);
            if (!methodName.Success) return CreateFailureResult(steps, overallStopwatch, methodName.ErrorMessage);
            artifacts.MethodName = methodName.Response;
            if (methodName.Status == GenerationStepStatus.Executed)
                conversation.Append(
                    GenerationStepType.MethodName,
                    CreateMethodNamePrompt(promptEvidence, artifacts.Scenario ?? string.Empty),
                    methodName.Response);

            // Step 3: Generate Arrange Plan
            var arrangePlan = request.Steps.EnableArrangePlan
                ? await ExecuteStructuredStepAsync<ArrangePlan>(
                    GenerationStepType.ArrangePlan,
                    PreparePrompt(
                        conversation,
                        request.ContextMode,
                        GenerationStepType.ArrangePlan,
                        CreateArrangePlanPrompt(promptEvidence, artifacts.Scenario ?? string.Empty, contextSummary)),
                    provider,
                    request.Temperature,
                    request.StepErrorRetries,
                    request.StepRetryDelayMs,
                    cancellationToken)
                : CreateFallbackStructuredStep<ArrangePlan>(
                    GenerationStepType.ArrangePlan,
                    "Arrange-plan step disabled; final synthesis will infer arrange code.");
            steps.Add(arrangePlan.Metadata);
            if (!arrangePlan.Metadata.Success)
                return CreateFailureResult(steps, overallStopwatch, arrangePlan.Metadata.ErrorMessage);
            artifacts.ArrangePlan = arrangePlan.Structured;
            if (arrangePlan.Metadata.Status == GenerationStepStatus.Executed)
                conversation.Append(
                    GenerationStepType.ArrangePlan,
                    CreateArrangePlanPrompt(promptEvidence, artifacts.Scenario ?? string.Empty, contextSummary),
                    arrangePlan.Metadata.Response);

            // Step 4: Generate Input Plan
            var inputPlan = request.Steps.EnableInputPlan
                ? await ExecuteStructuredStepAsync<InputPlan>(
                    GenerationStepType.InputPlan,
                    PreparePrompt(
                        conversation,
                        request.ContextMode,
                        GenerationStepType.InputPlan,
                        CreateInputPlanPrompt(promptEvidence, artifacts.Scenario ?? string.Empty, arrangePlan.Structured, contextSummary)),
                    provider,
                    request.Temperature,
                    request.StepErrorRetries,
                    request.StepRetryDelayMs,
                    cancellationToken)
                : CreateFallbackStructuredStep<InputPlan>(
                    GenerationStepType.InputPlan,
                    "Input-plan step disabled; final synthesis will infer concrete inputs.");
            steps.Add(inputPlan.Metadata);
            if (!inputPlan.Metadata.Success)
                return CreateFailureResult(steps, overallStopwatch, inputPlan.Metadata.ErrorMessage);
            artifacts.InputPlan = inputPlan.Structured;
            if (inputPlan.Metadata.Status == GenerationStepStatus.Executed)
                conversation.Append(
                    GenerationStepType.InputPlan,
                    CreateInputPlanPrompt(promptEvidence, artifacts.Scenario ?? string.Empty, arrangePlan.Structured, contextSummary),
                    inputPlan.Metadata.Response);

            // Step 5: Generate Action Plan
            var actionPlan = request.Steps.EnableActionPlan
                ? await ExecuteStructuredStepAsync<ActionPlan>(
                    GenerationStepType.ActionPlan,
                    PreparePrompt(
                        conversation,
                        request.ContextMode,
                        GenerationStepType.ActionPlan,
                        CreateActionPlanPrompt(promptEvidence, artifacts.Scenario ?? string.Empty, inputPlan.Structured)),
                    provider,
                    request.Temperature,
                    request.StepErrorRetries,
                    request.StepRetryDelayMs,
                    cancellationToken)
                : CreateFallbackStructuredStep<ActionPlan>(
                    GenerationStepType.ActionPlan,
                    "Action-plan step disabled; final synthesis will infer invocation.");
            steps.Add(actionPlan.Metadata);
            if (!actionPlan.Metadata.Success)
                return CreateFailureResult(steps, overallStopwatch, actionPlan.Metadata.ErrorMessage);
            artifacts.ActionPlan = actionPlan.Structured;
            if (actionPlan.Metadata.Status == GenerationStepStatus.Executed)
                conversation.Append(
                    GenerationStepType.ActionPlan,
                    CreateActionPlanPrompt(promptEvidence, artifacts.Scenario ?? string.Empty, inputPlan.Structured),
                    actionPlan.Metadata.Response);

            // Step 6: Generate Assertion Plan
            var assertionPlan = request.Steps.EnableAssertionPlan
                ? await ExecuteStructuredStepAsync<AssertionPlan>(
                    GenerationStepType.AssertionPlan,
                    PreparePrompt(
                        conversation,
                        request.ContextMode,
                        GenerationStepType.AssertionPlan,
                        CreateAssertionPlanPrompt(promptEvidence, artifacts.Scenario ?? string.Empty, actionPlan.Structured)),
                    provider,
                    request.Temperature,
                    request.StepErrorRetries,
                    request.StepRetryDelayMs,
                    cancellationToken)
                : CreateFallbackStructuredStep<AssertionPlan>(
                    GenerationStepType.AssertionPlan,
                    "Assertion-plan step disabled; final synthesis will infer assertions.");
            steps.Add(assertionPlan.Metadata);
            if (!assertionPlan.Metadata.Success)
                return CreateFailureResult(steps, overallStopwatch, assertionPlan.Metadata.ErrorMessage);
            artifacts.AssertionPlan = assertionPlan.Structured;
            if (assertionPlan.Metadata.Status == GenerationStepStatus.Executed)
                conversation.Append(
                    GenerationStepType.AssertionPlan,
                    CreateAssertionPlanPrompt(promptEvidence, artifacts.Scenario ?? string.Empty, actionPlan.Structured),
                    assertionPlan.Metadata.Response);

            // Step 7: Generate Final Test
            if (!request.Steps.EnableFinalTest)
            {
                steps.Add(CreateSkippedStep(
                    GenerationStepType.FinalTest,
                    "Final-test step disabled; no generated test can be produced."));
                return CreateFailureResult(steps, overallStopwatch, "Final-test step is disabled.");
            }

            var finalTestPrompt = CreateFinalTestPrompt(
                promptEvidence,
                artifacts.Scenario ?? string.Empty,
                artifacts.MethodName ?? BuildFallbackMethodName(promptEvidence),
                arrangePlan,
                inputPlan,
                actionPlan,
                assertionPlan,
                contextSummary);
            var finalTestStep = await ExecuteStepAsync(
                GenerationStepType.FinalTest,
                PreparePrompt(
                    conversation,
                    request.ContextMode,
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
            var extractedMethodName = Utilities.Utilities.ExtractTestMethodName(finalTest) ??
                                      TryExtractPatchTestMethodName(finalTest) ??
                                      ExtractMethodName(artifacts.MethodName ?? string.Empty);

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

    private async Task<TestGenerationResult> GenerateBasicExtensionOneShotAsync(
        TestGenerationRequest request,
        GenerationPromptEvidence promptEvidence,
        string contextSummary,
        List<GenerationStepMetadata> steps,
        GenerationConversationState conversation,
        IAiGenerationProvider provider,
        Stopwatch overallStopwatch,
        CancellationToken cancellationToken)
    {
        if (!request.Steps.EnableFinalTest)
        {
            steps.Add(CreateSkippedStep(
                GenerationStepType.FinalTest,
                "Final-test step disabled; no generated patch can be produced."));
            return CreateFailureResult(steps, overallStopwatch, "Final-test step is disabled.");
        }

        var finalTestPrompt = CreateBasicExtensionOneShotPatchPrompt(promptEvidence, contextSummary);
        var finalTestStep = await ExecuteStepAsync(
            GenerationStepType.FinalTest,
            PreparePrompt(
                conversation,
                request.ContextMode,
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
        var extractedMethodName = Utilities.Utilities.ExtractTestMethodName(finalTest)
                                  ?? TryExtractPatchTestMethodName(finalTest)
                                  ?? BuildFallbackMethodName(promptEvidence);

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

    private static bool ShouldUseBasicExtensionOneShot(TestGenerationRequest request)
    {
        return request.UseStructuredPatchOutput && !request.Steps.EnableSpeculativePlanning;
    }

    public async Task<TestGenerationResult> RepairTestAsync(
        TestRepairRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderAsync(request.Provider, cancellationToken);
        var steps = new List<GenerationStepMetadata>();
        var conversation = GenerationConversationState.Create(
            ShouldUseConversationHistory(request),
            request.PriorConversationTranscript);
        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            var repairStepType = DetermineRepairStepType(request);
            var rawRepairPrompt = CreateRepairPrompt(request);
            var repairPrompt = PreparePrompt(
                conversation,
                request.ContextMode,
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
            if (result.Success) conversation.Append(repairStepType, rawRepairPrompt, result.Response);

            if (!result.Success) return CreateFailureResult(steps, overallStopwatch, result.ErrorMessage);

            var repairedTest = ExtractCodeBlock(result.Response);
            // For Basic Extension patch repair the response is JSON, not raw C#.
            // Try Roslyn method-name extraction first; fall back to deserializing the patch.
            var testMethodName = Utilities.Utilities.ExtractTestMethodName(repairedTest)
                ?? TryExtractPatchTestMethodName(repairedTest);

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
            throw new InvalidOperationException($"Provider {providerType} not registered.");

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
                    _context.Project.Logger?.Information($"Executing step: {stepType}");
                else
                    _context.Project.Logger?.Warning(
                        "Retrying step {StepType} ({Attempt}/{MaxAttempts})",
                        stepType,
                        attempt,
                        maxAttempts);

                var response = await provider.GenerateAsync(prompt, temperature, cancellationToken);
                stopwatch.Stop();

                _context.Project.Logger?.Information(
                    $"Step {stepType} completed in {stopwatch.Elapsed.TotalSeconds:F2}s, {tokenCount} tokens");

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

                    if (stepRetryDelayMs > 0) await Task.Delay(stepRetryDelayMs, cancellationToken);

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
            Status = metadata.Status,
            SkipReason = metadata.SkipReason,
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
        if (string.IsNullOrWhiteSpace(json)) return (null, null);

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
        GenerationContextMode contextMode,
        GenerationStepType stepType,
        string prompt)
    {
        if (contextMode == GenerationContextMode.NoHistory || !conversation.Enabled) return prompt;

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

    private static bool ShouldUseConversationHistory(TestGenerationRequest request)
    {
        return request.ContextMode == GenerationContextMode.ChainedHistory;
    }

    private static bool ShouldUseConversationHistory(TestRepairRequest request)
    {
        return request.ContextMode == GenerationContextMode.ChainedHistory;
    }

    private static GenerationStepMetadata CreateFallbackStep(
        GenerationStepType stepType,
        string response,
        string reason)
    {
        return new GenerationStepMetadata
        {
            StepType = stepType,
            Status = GenerationStepStatus.Fallback,
            SkipReason = reason,
            Prompt = string.Empty,
            Response = response,
            ResponseFormat = "text/plain",
            PromptVersion = PromptVersion,
            ValidationStatus = "fallback",
            Success = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }

    private static GenerationStepMetadata CreateSkippedStep(
        GenerationStepType stepType,
        string reason)
    {
        return new GenerationStepMetadata
        {
            StepType = stepType,
            Status = GenerationStepStatus.Skipped,
            SkipReason = reason,
            Prompt = string.Empty,
            Response = string.Empty,
            ResponseFormat = GetResponseFormat(stepType),
            PromptVersion = PromptVersion,
            ValidationStatus = "skipped",
            Success = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }

    private static StructuredStepResult<TPlan> CreateFallbackStructuredStep<TPlan>(
        GenerationStepType stepType,
        string reason)
        where TPlan : class, new()
    {
        var structured = new TPlan();
        var json = JsonSerializer.Serialize(structured);
        return new StructuredStepResult<TPlan>(
            new GenerationStepMetadata
            {
                StepType = stepType,
                Status = GenerationStepStatus.Fallback,
                SkipReason = reason,
                Prompt = string.Empty,
                Response = json,
                ResponseFormat = "application/json",
                StructuredResponseJson = json,
                PromptVersion = PromptVersion,
                ValidationStatus = "fallback",
                Success = true,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            },
            structured);
    }

    private static GenerationStepMetadata CreateContextGraphStep(ContextGraph graph)
    {
        var json = JsonSerializer.Serialize(graph);
        return new GenerationStepMetadata
        {
            StepType = GenerationStepType.ContextGraph,
            Status = GenerationStepStatus.Executed,
            Prompt = string.Empty,
            Response = json,
            ResponseFormat = "application/json",
            StructuredResponseJson = json,
            PromptVersion = PromptVersion,
            ValidationStatus = "built",
            Success = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }

    private static GenerationStepMetadata CreateContextResolutionStep(
        IReadOnlyList<ContextResolutionResult> resolutions)
    {
        var json = JsonSerializer.Serialize(resolutions);
        return new GenerationStepMetadata
        {
            StepType = GenerationStepType.ContextResolution,
            Status = GenerationStepStatus.Executed,
            Prompt = string.Empty,
            Response = json,
            ResponseFormat = "application/json",
            StructuredResponseJson = json,
            PromptVersion = PromptVersion,
            ValidationStatus = "resolved",
            Success = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }

    private static string BuildContextSummary(GenerationPipelineArtifacts artifacts)
    {
        if (artifacts.ContextGraph == null) return "Context graph disabled or unavailable.";

        var graphLines = artifacts.ContextGraph.Nodes
            .Select(node =>
                $"- {node.NodeType}: {node.VariableName ?? node.TypeName} ({node.TypeName}) - {node.ConstructionHint}")
            .ToList();

        var resolutionLines = artifacts.ContextResolution.Count == 0
            ? ["- No context resolution snippets were produced."]
            : artifacts.ContextResolution.Select(result =>
                $"- {result.NodeId}: {result.CodeSnippet} {result.Explanation}".Trim()).ToList();

        return string.Join(
            Environment.NewLine,
            ["Context graph:", ..graphLines, "Context resolution:", ..resolutionLines]);
    }

    private static string BuildFallbackScenario(GenerationPromptEvidence evidence)
    {
        return $"Generate a focused test for {evidence.MethodName} using the available method and test context.";
    }

    private static string BuildFallbackMethodName(GenerationPromptEvidence evidence)
    {
        var methodName = string.IsNullOrWhiteSpace(evidence.MethodName)
            ? "Generated"
            : evidence.MethodName.Trim();

        return $"{methodName}_GeneratedScenario_ExpectedBehavior";
    }

    private string CreateScenarioPrompt(GenerationPromptEvidence request)
    {
        var mutationEvidence = BuildMutationEvidencePromptBlock(request);

        return $@"Given this method:
{request.MethodBody}

And this example test from the same test class:
{request.ExampleTest}

Full test file for local context:
{request.TestFileContents}

Relevant helper/setup members already present in that file:
{request.TestSupportContext}

Context graph access path:
{request.AccessPathSummary}

Example test metadata:
{request.ExampleTestMetadataSummary}

Project test metadata patterns:
{request.ProjectTestMetadataSummary}

Describe a specific test scenario that would extend coverage of the target method.
Focus on an edge case or untested path.
Use this coverage gap information when deciding which path to target:
{request.CoverageGapSummary}

{mutationEvidence}

Respond with only the scenario description, 1-2 sentences.";
    }

    private string CreateMethodNamePrompt(GenerationPromptEvidence request, string scenario)
    {
        return $@"Given this test scenario:
{scenario}

For this method:
{request.MethodSignature}

Generate an appropriate test method name following {request.TestFramework} conventions.
The name should clearly describe what is being tested.
Respond with only the method name, nothing else.";
    }

    private string CreateArrangePlanPrompt(
        GenerationPromptEvidence request,
        string scenario,
        string contextSummary)
    {
        var mutationEvidence = BuildMutationEvidencePromptBlock(request);

        return $@"For this test scenario:
{scenario}

And this method under test:
{request.MethodBody}

Context graph and resolution hints:
{contextSummary}

Candidate method test intentions:
{request.CandidateTestIntentionsSummary}

Type construction guidance:
{request.CandidateTypeConstructionSummary}

Selected access path:
{request.AccessPathSummary}

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

{mutationEvidence}

Respond with only JSON.";
    }

    private string CreateInputPlanPrompt(
        GenerationPromptEvidence request,
        string scenario,
        ArrangePlan arrangePlan,
        string contextSummary)
    {
        var mutationEvidence = BuildMutationEvidencePromptBlock(request);

        return $@"For this test scenario:
{scenario}

Method under test:
{request.MethodBody}

Arrange plan:
{JsonSerializer.Serialize(arrangePlan)}

Context graph and resolution hints:
{contextSummary}

Candidate method test intentions:
{request.CandidateTestIntentionsSummary}

Type construction guidance:
{request.CandidateTypeConstructionSummary}

Selected access path:
{request.AccessPathSummary}

Return strict JSON in this shape:
{{
  ""inputs"": [""var id = 1;""],
  ""preconditions"": [""seed repository with ...""]
}}

Relevant coverage gaps:
{request.CoverageGapSummary}

{mutationEvidence}

Respond with only JSON.";
    }

    private string CreateActionPlanPrompt(GenerationPromptEvidence request, string scenario, InputPlan inputPlan)
    {
        return $@"For this test scenario:
{scenario}

Method under test:
{request.MethodBody}

Input plan:
{JsonSerializer.Serialize(inputPlan)}

Selected access path:
{request.AccessPathSummary}

Return strict JSON in this shape:
{{
  ""invocation"": ""var result = sut.DoThing(input);"",
  ""resultBinding"": ""result""
}}

Respond with only JSON.";
    }

    private string CreateAssertionPlanPrompt(GenerationPromptEvidence request, string scenario, ActionPlan actionPlan)
    {
        var mutationEvidence = BuildMutationEvidencePromptBlock(request);

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

{mutationEvidence}

Return strict JSON in this shape:
{{
  ""assertions"": [""Assert.Equal(expected, result);""],
  ""expectedBehavior"": ""short explanation""
}}

Respond with only JSON.";
    }

    private string CreateFinalTestPrompt(
        GenerationPromptEvidence request,
        string scenario,
        string methodName,
        StructuredStepResult<ArrangePlan> arrangePlan,
        StructuredStepResult<InputPlan> inputPlan,
        StructuredStepResult<ActionPlan> actionPlan,
        StructuredStepResult<AssertionPlan> assertionPlan,
        string contextSummary)
    {
        if (request.UseStructuredPatchOutput)
            return CreateBasicExtensionPatchPrompt(
                request, scenario, methodName,
                arrangePlan, inputPlan, actionPlan, assertionPlan,
                contextSummary);

        var mutationEvidence = BuildMutationEvidencePromptBlock(request);

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

Context graph and resolution hints:
{contextSummary}

Candidate method test intentions:
{request.CandidateTestIntentionsSummary}

Type construction guidance:
{request.CandidateTypeConstructionSummary}

Selected access path:
{request.AccessPathSummary}

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

{mutationEvidence}

Requirements:
- Return a complete test method only, not a scaffold or outline
- Use the requested method name
- Include an appropriate test attribute for {request.TestFramework}
- Include an XML doc comment describing the scenario
- Include // Arrange, // Act, // Assert comments
- Follow the selected context graph access path. Do not call non-public target members directly unless the access path explicitly allows it.
- Ensure the code is valid C# and internally consistent
- Do not include any explanation outside the code block

Respond with only the complete test method code wrapped in ```csharp.";
    }

    private string CreateRepairPrompt(TestRepairRequest request)
    {
        // When UseStructuredPatchOutput is set this is a Basic Extension repair regardless of
        // failure type (application failure, build failure, or runtime/assertion failure).
        // ModifiedTestFileContents enriches the prompt when available but does not gate it.
        if (request.UseStructuredPatchOutput)
            return CreateBasicExtensionRepairPrompt(request);

        var mutationEvidence = BuildMutationEvidencePromptBlock(request.MetricsPath, request.MutationSummary);

        if (DetermineRepairStepType(request) == GenerationStepType.CompileRepair)
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

Context graph access path:
{request.AccessPathSummary}

Candidate method test intentions:
{request.CandidateTestIntentionsSummary}

Type construction guidance:
{request.CandidateTypeConstructionSummary}

The test class dependencies are:
{request.TestDependencies}

Example test metadata:
{request.ExampleTestMetadataSummary}

Project-wide test metadata patterns:
{request.ProjectTestMetadataSummary}

Coverage gaps for the target method are:
{request.CoverageGapSummary}

{mutationEvidence}

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

        return $@"I previously attempted to generate a test for this method:
{request.MethodBody}

The generated test was:
{request.GeneratedTest}

The full current test file is:
{request.TestFileContents}

Reusable helper/setup members already available:
{request.TestSupportContext}

Context graph access path:
{request.AccessPathSummary}

Candidate method test intentions:
{request.CandidateTestIntentionsSummary}

Type construction guidance:
{request.CandidateTypeConstructionSummary}

The build succeeded, but behavior or coverage was insufficient.

Observed execution/test feedback:
{request.ErrorLogs}

Coverage gaps for the target method are:
{request.CoverageGapSummary}

{mutationEvidence}

Focus only on behavior repair. Fix:
- wrong assertion logic
- brittle expectations
- weak input selection
- poor coverage targeting

Respond with only the complete test method code wrapped in ```.";
    }

    /// <summary>
    /// Builds the final-test prompt for Basic Extension mode.
    /// Instructs the model to return a <c>BasicExtensionPatch</c> JSON object
    /// containing only the additions needed for the new test (usings, helpers, test method).
    /// </summary>
    private string CreateBasicExtensionPatchPrompt(
        GenerationPromptEvidence request,
        string scenario,
        string methodName,
        StructuredStepResult<ArrangePlan> arrangePlan,
        StructuredStepResult<InputPlan> inputPlan,
        StructuredStepResult<ActionPlan> actionPlan,
        StructuredStepResult<AssertionPlan> assertionPlan,
        string contextSummary)
    {
        var mutationEvidence = BuildMutationEvidencePromptBlock(request);

        var existingUsings = ExtractExistingUsings(request.TestFileContents);
        var existingMethodNames = ExtractExistingMethodNames(request.TestFileContents, request.TestClass);
        var availablePackages = ExtractPackageReferences(request.TestProjectPath);

        var existingUsingsList = existingUsings.Count > 0
            ? string.Join("\n", existingUsings.Select(u => $"  {u}"))
            : "  (none)";

        var existingMethodsList = existingMethodNames.Count > 0
            ? string.Join("\n", existingMethodNames.Select(n => $"  {n}"))
            : "  (none)";

        var packagesList = availablePackages.Count > 0
            ? string.Join("\n", availablePackages.Select(p => $"  {p}"))
            : "  (none listed — use only well-known BCL namespaces and the source project's namespaces)";

        return $@"Write a new {request.TestFramework} test method to extend this existing test file.

Target method:
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

Context graph and resolution hints:
{contextSummary}

Candidate method test intentions:
{request.CandidateTestIntentionsSummary}

Type construction guidance:
{request.CandidateTypeConstructionSummary}

Selected access path:
{request.AccessPathSummary}

Example test for style and framework conventions:
{request.ExampleTest}

Example test metadata:
{request.ExampleTestMetadataSummary}

Project-wide test metadata patterns:
{request.ProjectTestMetadataSummary}

The test class (for structure reference):
{request.TestClass}

Full existing test file:
{request.TestFileContents}

Reusable helper/setup members already available:
{request.TestSupportContext}

Coverage gaps to target:
{request.CoverageGapSummary}

{mutationEvidence}

--- INTEGRATION CONSTRAINTS ---

Usings already in the file (do NOT add these to usingsToAdd):
{existingUsingsList}

Existing method names in the test class (do NOT add these to helperMethodsToAdd):
{existingMethodsList}

NuGet packages available in the test project (usingsToAdd must only reference namespaces
from these packages or from the source project — do not request packages not listed here):
{packagesList}

--- OUTPUT FORMAT ---

Return a JSON object matching this schema exactly, wrapped in ```json fences:

{{
  ""usingsToAdd"": [""Fully.Qualified.Namespace""],
  ""helperMethodsToAdd"": [""private static ReturnType HelperName(...) {{ ... }}""],
  ""testMethod"": ""[{request.TestFramework}Attribute]\npublic void {methodName}() {{ ... }}"",
  ""testMethodName"": ""{methodName}"",
  ""integrationRationale"": ""One sentence explaining what additions are needed and why.""
}}

Rules:
- usingsToAdd: list of fully-qualified namespace strings only. No ""using"" keyword, no semicolons.
  Only include namespaces NOT already in the file. Only use namespaces from the packages listed above.
- helperMethodsToAdd: complete C# method declarations. Only add helpers that are essential and not already present.
- testMethod: complete, valid C# method declaration including attributes and XML doc comment.
  Include // Arrange, // Act, // Assert comments. Follow {request.TestFramework} conventions.
- testMethodName: must match the method name declared in testMethod.
- All usingsToAdd and helperMethodsToAdd entries must be valid C# that can be inserted verbatim.
- Do not reproduce the full test file. Do not modify existing tests.
- Do not include any explanation outside the JSON block.

Respond with only the JSON object wrapped in ```json.";
    }

    private string CreateBasicExtensionOneShotPatchPrompt(
        GenerationPromptEvidence request,
        string contextSummary)
    {
        var mutationEvidence = BuildMutationEvidencePromptBlock(request);

        var existingUsings = ExtractExistingUsings(request.TestFileContents);
        var existingMethodNames = ExtractExistingMethodNames(request.TestFileContents, request.TestClass);
        var availablePackages = ExtractPackageReferences(request.TestProjectPath);

        var existingUsingsList = existingUsings.Count > 0
            ? string.Join("\n", existingUsings.Select(u => $"  {u}"))
            : "  (none)";

        var existingMethodsList = existingMethodNames.Count > 0
            ? string.Join("\n", existingMethodNames.Select(n => $"  {n}"))
            : "  (none)";

        var packagesList = availablePackages.Count > 0
            ? string.Join("\n", availablePackages.Select(p => $"  {p}"))
            : "  (none listed - use only well-known BCL namespaces and the source project's namespaces)";

        return $@"Add exactly one new {request.TestFramework} test method to this existing test file.

Use the evidence below to choose the best single behavior to test. Prefer one valid,
terminating, high-confidence test over a broad brittle test. Do not try to cover every
uncovered line if that requires guessing hidden input or setup details.

Target method:
{request.MethodBody}

Context graph and resolution hints:
{contextSummary}

Candidate method test intentions:
{request.CandidateTestIntentionsSummary}

Type construction guidance:
{request.CandidateTypeConstructionSummary}

Selected access path:
{request.AccessPathSummary}

Example test for style and framework conventions:
{request.ExampleTest}

Example test metadata:
{request.ExampleTestMetadataSummary}

Project-wide test metadata patterns:
{request.ProjectTestMetadataSummary}

The test class (for structure reference):
{request.TestClass}

Full existing test file:
{request.TestFileContents}

Reusable helper/setup members already available:
{request.TestSupportContext}

Coverage gaps to target:
{request.CoverageGapSummary}

{mutationEvidence}

--- INTEGRATION CONSTRAINTS ---

Usings already in the file (do NOT add these to usingsToAdd):
{existingUsingsList}

Existing method names in the test class (do NOT add these to helperMethodsToAdd or reuse as the test method name):
{existingMethodsList}

NuGet packages available in the test project (usingsToAdd must only reference namespaces
from these packages or from the source project; do not request packages not listed here):
{packagesList}

--- OUTPUT FORMAT ---

Return a JSON object matching this schema exactly, wrapped in ```json fences:

{{
  ""usingsToAdd"": [""Fully.Qualified.Namespace""],
  ""helperMethodsToAdd"": [""private static ReturnType HelperName(...) {{ ... }}""],
  ""testMethod"": ""[{request.TestFramework}Attribute]\npublic void TestMethodName() {{ ... }}"",
  ""testMethodName"": ""TestMethodName"",
  ""integrationRationale"": ""One sentence explaining what additions are needed and why.""
}}

Rules:
- Generate exactly one test method.
- Choose the test method name yourself. It must not match any existing method name.
- usingsToAdd: list of fully-qualified namespace strings only. No ""using"" keyword, no semicolons.
  Only include namespaces NOT already in the file. Only use namespaces from the packages listed above.
- helperMethodsToAdd: complete C# method declarations. Only add helpers that are essential and not already present.
- testMethod: complete, valid C# method declaration including attributes and XML doc comment.
  Include // Arrange, // Act, // Assert comments. Follow {request.TestFramework} conventions.
- Do not reproduce the full test file. Do not modify existing tests.
- Do not include any explanation outside the JSON block.

Respond with only the JSON object wrapped in ```json.";
    }

    /// <summary>
    /// Extracts the fully-qualified using directive strings from the parsed test file contents.
    /// Returns strings like <c>"using System.IO;"</c>.
    /// </summary>
    private static IReadOnlyList<string> ExtractExistingUsings(string fileContents)
    {
        if (string.IsNullOrWhiteSpace(fileContents)) return [];

        try
        {
            var root = CSharpSyntaxTree.ParseText(fileContents).GetCompilationUnitRoot();
            return root.Usings
                .Select(u => u.ToString().Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Extracts the method names from the target test class inside the test file.
    /// Falls back to all classes when <paramref name="testClassName"/> is empty or unmatched.
    /// </summary>
    private static IReadOnlyList<string> ExtractExistingMethodNames(
        string fileContents,
        string testClassName)
    {
        if (string.IsNullOrWhiteSpace(fileContents)) return [];

        try
        {
            var root = CSharpSyntaxTree.ParseText(fileContents).GetCompilationUnitRoot();

            var targetClass = string.IsNullOrWhiteSpace(testClassName)
                ? null
                : root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == testClassName);

            var members = targetClass != null
                ? targetClass.Members.OfType<MethodDeclarationSyntax>()
                : root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            return members
                .Select(m => m.Identifier.Text)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Reads <c>&lt;PackageReference Include="..." /&gt;</c> entries from a <c>.csproj</c> file.
    /// Returns an empty list if the file does not exist or cannot be parsed.
    /// </summary>
    private static IReadOnlyList<string> ExtractPackageReferences(string csprojPath)
    {
        if (string.IsNullOrWhiteSpace(csprojPath) || !File.Exists(csprojPath))
            return [];

        try
        {
            return XDocument.Load(csprojPath)
                .Descendants("PackageReference")
                .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Builds the repair prompt for Basic Extension mode.
    /// Used for all failure types in the BasicExtension patch path:
    /// <list type="bullet">
    ///   <item>Application failure — patch could not be applied (e.g. duplicate method, malformed JSON):
    ///   shows original file + error; <see cref="TestRepairRequest.ModifiedTestFileContents"/> is null.</item>
    ///   <item>Build failure — patch applied but compilation failed:
    ///   shows original file + integrated post-patch state + structured diagnostics.</item>
    ///   <item>Runtime/assertion failure — compiled and ran but test did not pass:
    ///   shows original file + runtime feedback; <see cref="TestRepairRequest.ModifiedTestFileContents"/> is null.</item>
    /// </list>
    /// In all cases the model is asked to return a corrected <c>BasicExtensionPatch</c> JSON applied
    /// to the original (pre-patch) file. <see cref="TestRepairRequest.ModifiedTestFileContents"/>
    /// enriches the prompt when present but does not determine the output format.
    /// </summary>
    private string CreateBasicExtensionRepairPrompt(TestRepairRequest request)
    {
        var hasIntegratedSnapshot = !string.IsNullOrWhiteSpace(request.ModifiedTestFileContents);
        var hasStructuredErrors = !string.IsNullOrWhiteSpace(request.StructuredErrors);

        // Repair objective adapts to the failure type:
        // build failure → fix compilation; application/runtime failure → fix patch or test logic.
        var repairObjective = hasIntegratedSnapshot
            ? "Fix only the compilation and integration errors shown below. Preserve the intended test behavior."
            : "Return a corrected patch that can be applied cleanly and produces a passing test.";

        // Shown only for build failures — the file as it looked after the failed patch was applied.
        var postPatchBlock = hasIntegratedSnapshot
            ? $"\n\nPost-patch test file (the integrated state after the previous patch was applied — use this to understand the compilation errors):\n{request.ModifiedTestFileContents}"
            : string.Empty;

        // Shown only when structured compiler diagnostics are available (build failure path).
        var diagnosticsBlock = hasStructuredErrors
            ? $"\n\nStructured compiler/build diagnostics:\n{request.StructuredErrors}"
            : string.Empty;

        // Shown for repair attempts 3+ only: earlier failures the model wouldn't otherwise
        // see (the immediately previous attempt is already covered by GeneratedTest + ErrorLogs).
        var priorAttemptsBlock = !string.IsNullOrWhiteSpace(request.PriorAttemptsSummary)
            ? $"\n\n{request.PriorAttemptsSummary}"
            : string.Empty;

        return $@"The previous Basic Extension patch did not produce a passing test. {repairObjective}
The corrected patch will be applied to the original test file shown below.
Do not modify production code, project files, or unrelated tests.

Target method:
{request.MethodBody}

Original test file (before patch application — the corrected patch will be applied to this):
{request.TestFileContents}{postPatchBlock}

Previous patch (reference only — do not reproduce it verbatim):
{request.GeneratedTest}

Test class dependencies:
{request.TestDependencies}

Example test metadata:
{request.ExampleTestMetadataSummary}

Failure details:
{request.ErrorLogs}{diagnosticsBlock}{priorAttemptsBlock}

--- OUTPUT FORMAT ---

Return a corrected JSON object matching this schema exactly, wrapped in ```json fences:

{{
  ""usingsToAdd"": [""Fully.Qualified.Namespace""],
  ""helperMethodsToAdd"": [""private static ReturnType HelperName(...) {{ ... }}""],
  ""testMethod"": ""[TestAttribute]\npublic void TestMethodName() {{ ... }}"",
  ""testMethodName"": ""TestMethodName"",
  ""integrationRationale"": ""One sentence explaining the corrections made.""
}}

Rules:
- Fix only the issues visible in the failure details above.
- Do not change the test's intended behavior or rename the test method unless the name itself caused the failure.
- usingsToAdd: only include namespaces NOT already in the original file.
- helperMethodsToAdd: only include new helpers not already in the original file.
- testMethod: the corrected, complete test method including attributes and XML doc comment.
- testMethodName: must match the method name declared in testMethod.
- Respond with only the JSON object wrapped in ```json.";
    }

    /// <summary>
    /// Tries to extract the test method name from a <c>BasicExtensionPatch</c> JSON response.
    /// Used as a fallback when Roslyn extraction fails (e.g., because the response is JSON, not raw C#).
    /// Returns null if the response is not a valid patch or has no <c>testMethodName</c>.
    /// </summary>
    private static string? TryExtractPatchTestMethodName(string generatedTest)
    {
        var json = ExtractJson(generatedTest);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var patch = JsonSerializer.Deserialize<BasicExtensionPatch>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return string.IsNullOrWhiteSpace(patch?.TestMethodName) ? null : patch.TestMethodName;
        }
        catch
        {
            return null;
        }
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
            if (parts.Length >= 2) return parts[1].Replace("csharp", "").Trim();
        }

        return response.Trim();
    }

    private static string BuildMutationEvidencePromptBlock(GenerationPromptEvidence request)
    {
        return BuildMutationEvidencePromptBlock(request.MetricsPath, request.MutationSummary);
    }

    private static string BuildMutationEvidencePromptBlock(MetricsDrivenPath? metricsPath, string mutationSummary)
    {
        if (metricsPath is not (MetricsDrivenPath.Mutation or MetricsDrivenPath.CoverageAndMutation))
            return string.Empty;

        return $@"Mutation context:
{mutationSummary}";
    }

    private string ExtractMethodName(string response)
    {
        var cleaned = response.Trim();

        if (cleaned.StartsWith("Test_") || cleaned.StartsWith("test_") ||
            cleaned.Contains("Should") || cleaned.Contains("_"))
            return cleaned;

        return cleaned;
    }

    private static string ExtractJson(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return string.Empty;

        var trimmed = response.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var parts = trimmed.Split("```", StringSplitOptions.RemoveEmptyEntries);
            var candidate = parts.FirstOrDefault(x => x.Contains('{'));
            if (!string.IsNullOrWhiteSpace(candidate))
                trimmed = candidate.Replace("json", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
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
        if (!hasResponse) return "empty";

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
                state._entries.Add(existingTranscript.Trim());

            return state;
        }

        public void Append(GenerationStepType stepType, string prompt, string response)
        {
            if (!Enabled) return;

            _entries.Add($@"[{stepType}] User:
{prompt}

[{stepType}] Assistant:
{response}");
        }

        public string? Export()
        {
            if (!Enabled || _entries.Count == 0) return null;

            return string.Join("\n\n", _entries);
        }
    }

    private sealed record StructuredStepResult<TPlan>(GenerationStepMetadata Metadata, TPlan Structured);

    private sealed class GenerationPromptEvidence
    {
        public required string MethodBody { get; init; }
        public required string MethodName { get; init; }
        public required string MethodSignature { get; init; }
        public required string ContainingClass { get; init; }
        public required string ExampleTest { get; init; }
        public required string ExampleTestMetadataSummary { get; init; }
        public required string ProjectTestMetadataSummary { get; init; }
        public required string TestClass { get; init; }
        public required string TestFileContents { get; init; }
        public required string TestSupportContext { get; init; }
        public required string TestFramework { get; init; }
        public required string TestDependencies { get; init; }
        public MetricsDrivenPath? MetricsPath { get; init; }
        public required string CoverageGapSummary { get; init; }
        public required string MutationSummary { get; init; }
        public required string CandidateTestIntentionsSummary { get; init; }
        public required string CandidateTypeConstructionSummary { get; init; }
        public required string AccessPathSummary { get; init; }
        public bool UseStructuredPatchOutput { get; init; }
        public string TestProjectPath { get; init; } = string.Empty;

        public static GenerationPromptEvidence FromRequest(TestGenerationRequest request)
        {
            return new GenerationPromptEvidence
            {
                MethodBody = request.MethodBody,
                MethodName = request.MethodName,
                MethodSignature = request.MethodSignature,
                ContainingClass = request.ContainingClass,
                ExampleTest = request.ExampleTest,
                ExampleTestMetadataSummary = request.ExampleTestMetadataSummary,
                ProjectTestMetadataSummary = request.ProjectTestMetadataSummary,
                TestClass = request.TestClass,
                TestFileContents = request.TestFileContents,
                TestSupportContext = request.TestSupportContext,
                TestFramework = request.TestFramework,
                TestDependencies = request.TestDependencies,
                MetricsPath = request.MetricsPath,
                CoverageGapSummary = request.CoverageGapSummary,
                MutationSummary = request.MutationSummary,
                CandidateTestIntentionsSummary = request.CandidateTestIntentionsSummary,
                CandidateTypeConstructionSummary = request.CandidateTypeConstructionSummary,
                AccessPathSummary = request.AccessPathSummary,
                UseStructuredPatchOutput = request.UseStructuredPatchOutput,
                TestProjectPath = request.TestProjectPath
            };
        }
    }

    #endregion
}
