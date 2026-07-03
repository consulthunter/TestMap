using TestMap.Models.Rules;

namespace TestMap.Services.Rules;

public interface IRuleDecisionRecorder
{
    Task RecordAsync(
        int projectId,
        RuleDecisionScope scope,
        IEnumerable<RuleDecisionRecord> decisions,
        int? experimentRunId = null,
        int? candidateMethodId = null,
        int? generationAttemptId = null,
        int? testExecutionId = null,
        CancellationToken cancellationToken = default);

    string CreateSnapshotJson(IEnumerable<RuleDecisionRecord> decisions);
}
