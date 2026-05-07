using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Rules;
using TestMap.Persistence.Ef.Entities.Rules;

namespace TestMap.Persistence.Ef.Configuration.Entities.Rules;

public class RuleDecisionEntityConfiguration : IEntityTypeConfiguration<RuleDecisionEntity>
{
    public void Configure(EntityTypeBuilder<RuleDecisionEntity> builder)
    {
        builder.ToTable("rule_decisions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.CSharpProjectId).HasColumnName("csharp_project_id");
        builder.Property(x => x.ScopeKind).HasColumnName("scope_kind").IsRequired();
        builder.Property(x => x.ScopeId).HasColumnName("scope_id").IsRequired();
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id");
        builder.Property(x => x.CandidateMethodId).HasColumnName("candidate_method_id");
        builder.Property(x => x.GenerationAttemptId).HasColumnName("generation_attempt_id");
        builder.Property(x => x.TestExecutionId).HasColumnName("test_execution_id");
        builder.Property(x => x.DecisionKind).HasColumnName("decision_kind").IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").IsRequired();
        builder.Property(x => x.RuleId).HasColumnName("rule_id").IsRequired();
        builder.Property(x => x.RuleVersion).HasColumnName("rule_version").IsRequired();
        builder.Property(x => x.Confidence)
            .HasColumnName("confidence")
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.Evidence)
            .HasColumnName("evidence")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<RuleEvidenceRecord>>(v, (JsonSerializerOptions?)null) ?? new List<RuleEvidenceRecord>())
            .IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.CSharpProjectId);
        builder.HasIndex(x => new { x.ScopeKind, x.ScopeId });
        builder.HasIndex(x => x.ExperimentRunId);
        builder.HasIndex(x => x.CandidateMethodId);
        builder.HasIndex(x => x.GenerationAttemptId);
        builder.HasIndex(x => x.TestExecutionId);
        builder.HasIndex(x => new { x.RuleId, x.RuleVersion });
    }
}
