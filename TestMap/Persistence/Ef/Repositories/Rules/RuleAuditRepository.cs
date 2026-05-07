using Microsoft.EntityFrameworkCore;
using TestMap.Models.Rules;
using TestMap.Persistence.Ef.Entities.Rules;

namespace TestMap.Persistence.Ef.Repositories.Rules;

public class RuleAuditRepository
{
    private readonly TestMapDbContext _context;

    public RuleAuditRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task UpsertRuleDefinitionsAsync(IEnumerable<RuleDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            var existing = await _context.RuleDefinitions.FirstOrDefaultAsync(x =>
                x.RuleId == definition.Id &&
                x.RuleVersion == definition.Version);

            if (existing == null)
            {
                _context.RuleDefinitions.Add(new RuleDefinitionEntity
                {
                    RuleId = definition.Id,
                    RuleVersion = definition.Version,
                    Name = definition.Name,
                    Description = definition.Description,
                    Category = definition.Category
                });
                continue;
            }

            existing.Name = definition.Name;
            existing.Description = definition.Description;
            existing.Category = definition.Category;
        }

        await _context.SaveChangesAsync();
    }

    public async Task ReplaceProjectDecisionsAsync(
        int projectId,
        int? cSharpProjectId,
        IEnumerable<RuleDecisionRecord> decisions)
    {
        var existing = _context.RuleDecisions.Where(x =>
            x.ProjectId == projectId &&
            x.CSharpProjectId == cSharpProjectId);
        _context.RuleDecisions.RemoveRange(existing);

        var now = DateTime.UtcNow;
        _context.RuleDecisions.AddRange(decisions.Select(decision => new RuleDecisionEntity
        {
            ProjectId = projectId,
            CSharpProjectId = cSharpProjectId,
            ScopeKind = "Project",
            ScopeId = projectId.ToString(),
            DecisionKind = decision.DecisionKind,
            Value = decision.Value,
            RuleId = decision.RuleId,
            RuleVersion = decision.RuleVersion,
            Confidence = decision.Confidence,
            Evidence = decision.Evidence,
            Notes = decision.Notes,
            CreatedAt = now
        }));

        await _context.SaveChangesAsync();
    }

    public async Task AddScopedDecisionsAsync(
        int projectId,
        string scopeKind,
        string scopeId,
        IEnumerable<RuleDecisionRecord> decisions,
        int? experimentRunId = null,
        int? candidateMethodId = null,
        int? generationAttemptId = null,
        int? testExecutionId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        _context.RuleDecisions.AddRange(decisions.Select(decision => new RuleDecisionEntity
        {
            ProjectId = projectId,
            ScopeKind = scopeKind,
            ScopeId = scopeId,
            ExperimentRunId = experimentRunId,
            CandidateMethodId = candidateMethodId,
            GenerationAttemptId = generationAttemptId,
            TestExecutionId = testExecutionId,
            DecisionKind = decision.DecisionKind,
            Value = decision.Value,
            RuleId = decision.RuleId,
            RuleVersion = decision.RuleVersion,
            Confidence = decision.Confidence,
            Evidence = decision.Evidence,
            Notes = decision.Notes,
            CreatedAt = now
        }));

        await _context.SaveChangesAsync(cancellationToken);
    }
}
