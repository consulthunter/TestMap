using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;

namespace TestMap.Services.TestGeneration;

/// <summary>
/// Shared service for decomposed test generation pipeline.
/// Used by both regular test generation and AI provider comparison experiments.
/// </summary>
public interface ITestGenerationPipelineService
{
    /// <summary>
    /// Executes the full decomposed test generation pipeline for a single method.
    /// </summary>
    /// <param name="request">The generation request containing method info and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing generated test and metadata for each step</returns>
    Task<TestGenerationResult> GenerateTestAsync(
        TestGenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Repairs a failed test by providing compilation/execution errors.
    /// </summary>
    /// <param name="request">The repair request containing original test and error logs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing repaired test and metadata</returns>
    Task<TestGenerationResult> RepairTestAsync(
        TestRepairRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for generating a new test.
/// </summary>
public class TestGenerationRequest
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
    public required string CoverageGapSummary { get; init; }
    public required AiProvider Provider { get; init; }
    public double Temperature { get; init; } = 0.0;
    public int StepErrorRetries { get; init; }
    public int StepRetryDelayMs { get; init; } = 1000;
    public bool EnableHistoryChaining { get; init; }
}

/// <summary>
/// Request for repairing a failing test.
/// </summary>
public class TestRepairRequest
{
    public required string MethodBody { get; init; }
    public required string MethodName { get; init; }
    public required string GeneratedTest { get; init; }
    public required string TestClass { get; init; }
    public required string TestFramework { get; init; }
    public required string TestDependencies { get; init; }
    public required string TestFileContents { get; init; }
    public required string TestSupportContext { get; init; }
    public required string ExampleTestMetadataSummary { get; init; }
    public required string ProjectTestMetadataSummary { get; init; }
    public required string CoverageGapSummary { get; init; }
    public required string ErrorLogs { get; init; }
    public string? StructuredErrors { get; init; }
    public string? PriorConversationTranscript { get; init; }
    public required AiProvider Provider { get; init; }
    public double Temperature { get; init; } = 0.0;
    public int AttemptNumber { get; init; }
    public int StepErrorRetries { get; init; }
    public int StepRetryDelayMs { get; init; } = 1000;
    public bool EnableHistoryChaining { get; init; }
}

/// <summary>
/// Result of test generation including all step metadata.
/// </summary>
public class TestGenerationResult
{
    public bool Success { get; init; }
    public string? GeneratedTest { get; init; }
    public string? TestMethodName { get; init; }
    public List<GenerationStepMetadata> Steps { get; init; } = new();
    public double TotalDurationSeconds { get; init; }
    public int TotalTokens { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ConversationTranscript { get; init; }
}

/// <summary>
/// Metadata for a single generation step.
/// </summary>
public class GenerationStepMetadata
{
    public GenerationStepType StepType { get; init; }
    public required string Prompt { get; init; }
    public required string Response { get; init; }
    public string? ResponseFormat { get; init; }
    public string? StructuredResponseJson { get; init; }
    public string? PromptVersion { get; init; }
    public string? ValidationStatus { get; init; }
    public int TokenCount { get; init; }
    public double DurationSeconds { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}