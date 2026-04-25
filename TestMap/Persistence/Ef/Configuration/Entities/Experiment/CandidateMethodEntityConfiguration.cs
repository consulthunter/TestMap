using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class CandidateMethodEntityConfiguration : IEntityTypeConfiguration<CandidateMethodEntity>
{
    public void Configure(EntityTypeBuilder<CandidateMethodEntity> builder)
    {
        builder.ToTable("candidate_methods");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id").IsRequired();
        builder.Property(x => x.SourceMemberId).HasColumnName("source_member_id").IsRequired();
        builder.Property(x => x.ExistingTestMemberId).HasColumnName("existing_test_member_id");
        builder.Property(x => x.SourceMethodName).HasColumnName("source_method_name").IsRequired();
        builder.Property(x => x.SourceMethodSignature).HasColumnName("source_method_signature").IsRequired();
        builder.Property(x => x.ExistingTestMethodName).HasColumnName("existing_test_method_name");
        builder.Property(x => x.InitialCoverage).HasColumnName("initial_coverage").IsRequired();
        builder.Property(x => x.InitialCoveredLines).HasColumnName("initial_covered_lines").IsRequired();
        builder.Property(x => x.InitialTotalLines).HasColumnName("initial_total_lines").IsRequired();
        builder.Property(x => x.MetricDrivenScore).HasColumnName("metric_driven_score");
        builder.Property(x => x.ExpectedMetricDelta).HasColumnName("expected_metric_delta");
        builder.Property(x => x.MetricConfidence).HasColumnName("metric_confidence");
        builder.Property(x => x.MetricFeasibility).HasColumnName("metric_feasibility");
        builder.Property(x => x.MetricEstimatedCost).HasColumnName("metric_estimated_cost");
        builder.Property(x => x.MetricGuardrailStatus).HasColumnName("metric_guardrail_status");
        builder.Property(x => x.MetricSelectionReason).HasColumnName("metric_selection_reason");
        builder.Property(x => x.TestImprovementScore).HasColumnName("test_improvement_score");
        builder.Property(x => x.TestImprovementReason).HasColumnName("test_improvement_reason");
        builder.Property(x => x.TestState).HasColumnName("test_state");
        builder.Property(x => x.RecommendedAction).HasColumnName("recommended_action");
        builder.Property(x => x.TestStateReason).HasColumnName("test_state_reason");
        builder.Property(x => x.SelectionTime).HasColumnName("selection_time").IsRequired();

        builder.HasMany(x => x.GenerationAttempts)
            .WithOne(x => x.CandidateMethod)
            .HasForeignKey(x => x.CandidateMethodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}