using TestMap.Models.Experiment.Assertions;

namespace TestMap.Services.StaticAnalysis.Assertions;

public interface IAssertionLineageAnalysisService
{
    Task<AssertionLineageAnalysisResult> AnalyzeAsync(
        AssertionLineageAnalysisRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AssertionLineageAnalysisRequest
{
    public required int ProjectId { get; init; }
    public required int SolutionId { get; init; }
    public required int IntendedSourceMemberId { get; init; }
    public required IReadOnlyList<int> TestMemberIds { get; init; }
    public required AssertionLineagePolicy Policy { get; init; }
}

public sealed class AssertionLineageAnalysisResult
{
    public bool Available { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
    public required AssertionLineagePolicy Policy { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public double AnalysisDurationMs { get; init; }
    public IReadOnlyList<GeneratedTestAssertionSummary> TestSummaries { get; init; } = [];

    public static AssertionLineageAnalysisResult Unavailable(
        AssertionLineagePolicy policy,
        string failureCode,
        string failureReason,
        DateTime startedAt,
        DateTime completedAt)
    {
        return new AssertionLineageAnalysisResult
        {
            Available = false,
            FailureCode = failureCode,
            FailureReason = failureReason,
            Policy = policy,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            AnalysisDurationMs = (completedAt - startedAt).TotalMilliseconds
        };
    }
}
