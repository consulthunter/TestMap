using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Strategies;

public interface ITestGenerationApproach
{
    TestGenerationApproach Strategy { get; }

    bool ShouldSkipGeneration(CandidateMethodContext context);

    TestGenerationRequest CreateGenerationRequest(TestGenerationApproachContext context);

    TestRepairRequest CreateRepairRequest(TestRepairApproachContext context);
}

public sealed class TestGenerationApproachContext
{
    public required CandidateMethodContext MethodContext { get; init; }
    public required AiProvider Provider { get; init; }
    public double Temperature { get; init; }
    public int StepErrorRetries { get; init; }
    public int StepRetryDelayMs { get; init; } = 1000;
}

public sealed class TestRepairApproachContext
{
    public required CandidateMethodContext MethodContext { get; init; }
    public required string GeneratedTest { get; init; }
    public required string ErrorLogs { get; init; }
    public string? StructuredErrors { get; init; }
    public string? PriorConversationTranscript { get; init; }
    /// <summary>
    /// Compact one-line-per-attempt summary of every prior failure in this repair chain.
    /// Included in the repair prompt so the model knows which approaches have already been
    /// tried and should not be repeated. Built by the orchestration layer from the
    /// <see cref="GenerationAttempt"/> history; null when not applicable (e.g. first repair).
    /// </summary>
    public string? PriorAttemptsSummary { get; init; }
    /// <summary>
    /// The test file content after patch application, if a Basic Extension patch was applied
    /// before the build failed. When set, used in place of <see cref="CandidateMethodContext.TestFileContents"/>
    /// for the repair prompt so the model sees the actual integrated state that failed to compile.
    /// </summary>
    public string? ModifiedTestFileContents { get; init; }
    public required AiProvider Provider { get; init; }
    public double Temperature { get; init; }
    public int AttemptNumber { get; init; }
    public int StepErrorRetries { get; init; }
    public int StepRetryDelayMs { get; init; } = 1000;
}
