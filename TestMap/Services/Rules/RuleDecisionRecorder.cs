using System.Text.Json;
using TestMap.Models.Rules;
using TestMap.Persistence.Ef.Repositories.Rules;
using TestMap.Rules;

namespace TestMap.Services.Rules;

public sealed class RuleDecisionRecorder : IRuleDecisionRecorder
{
    private readonly RuleAuditRepository _repository;

    public RuleDecisionRecorder(RuleAuditRepository repository)
    {
        _repository = repository;
    }

    public async Task RecordAsync(
        int projectId,
        RuleDecisionScope scope,
        IEnumerable<RuleDecisionRecord> decisions,
        int? experimentRunId = null,
        int? candidateMethodId = null,
        int? generationAttemptId = null,
        int? testExecutionId = null,
        CancellationToken cancellationToken = default)
    {
        var decisionList = decisions.ToList();
        if (projectId <= 0 || decisionList.Count == 0) return;

        await _repository.UpsertRuleDefinitionsAsync(RuleDefinitionRegistry.All);
        await _repository.AddScopedDecisionsAsync(
            projectId,
            scope.Kind,
            scope.Id,
            decisionList,
            experimentRunId,
            candidateMethodId,
            generationAttemptId,
            testExecutionId,
            cancellationToken);
    }

    public string CreateSnapshotJson(IEnumerable<RuleDecisionRecord> decisions)
    {
        return JsonSerializer.Serialize(decisions);
    }
}
