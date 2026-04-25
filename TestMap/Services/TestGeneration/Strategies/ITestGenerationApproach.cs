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
    public bool EnableHistoryChaining { get; init; }
}

public sealed class TestRepairApproachContext
{
    public required CandidateMethodContext MethodContext { get; init; }
    public required string GeneratedTest { get; init; }
    public required string ErrorLogs { get; init; }
    public string? StructuredErrors { get; init; }
    public string? PriorConversationTranscript { get; init; }
    public required AiProvider Provider { get; init; }
    public double Temperature { get; init; }
    public int AttemptNumber { get; init; }
    public int StepErrorRetries { get; init; }
    public int StepRetryDelayMs { get; init; } = 1000;
    public bool EnableHistoryChaining { get; init; }
}