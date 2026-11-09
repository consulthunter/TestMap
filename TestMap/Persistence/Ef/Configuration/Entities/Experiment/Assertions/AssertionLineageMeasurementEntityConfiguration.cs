using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment.Assertions;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment.Assertions;

public sealed class AssertionLineageMeasurementEntityConfiguration
    : IEntityTypeConfiguration<AssertionLineageMeasurementEntity>
{
    public void Configure(EntityTypeBuilder<AssertionLineageMeasurementEntity> builder)
    {
        builder.ToTable(
            "assertion_lineage_measurements",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_assertion_lineage_measurements_single_owner",
                    "(generation_attempt_id IS NOT NULL AND tool_attempt_id IS NULL) OR " +
                    "(generation_attempt_id IS NULL AND tool_attempt_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_assertion_lineage_measurements_positive_depth",
                    "max_depth > 0");
                table.HasCheckConstraint(
                    "ck_assertion_lineage_measurements_category_counts",
                    "recognized_assertion_count IS NULL OR " +
                    "recognized_assertion_count = traced_assertion_count + trivial_assertion_count + unresolved_assertion_count");
            });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id").IsRequired();
        builder.Property(x => x.CandidateMethodId).HasColumnName("candidate_method_id").IsRequired();
        builder.Property(x => x.GenerationAttemptId).HasColumnName("generation_attempt_id");
        builder.Property(x => x.ToolAttemptId).HasColumnName("tool_attempt_id");
        builder.Property(x => x.ProducerLane).HasColumnName("producer_lane").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.FailureCode).HasColumnName("failure_code");
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason");
        builder.Property(x => x.PolicyVersion).HasColumnName("policy_version").IsRequired();
        builder.Property(x => x.AssertionCatalogVersion).HasColumnName("assertion_catalog_version").IsRequired();
        builder.Property(x => x.MaxDepth).HasColumnName("max_depth").IsRequired();
        builder.Property(x => x.EligibleTestCount).HasColumnName("eligible_test_count");
        builder.Property(x => x.AnalyzedTestCount).HasColumnName("analyzed_test_count");
        builder.Property(x => x.UnavailableTestCount).HasColumnName("unavailable_test_count");
        builder.Property(x => x.NoRecognizedAssertionTestCount)
            .HasColumnName("no_recognized_assertion_test_count");
        builder.Property(x => x.RecognizedAssertionCount).HasColumnName("recognized_assertion_count");
        builder.Property(x => x.UnrecognizedAssertionCount).HasColumnName("unrecognized_assertion_count");
        builder.Property(x => x.TracedAssertionCount).HasColumnName("traced_assertion_count");
        builder.Property(x => x.TrivialAssertionCount).HasColumnName("trivial_assertion_count");
        builder.Property(x => x.UnresolvedAssertionCount).HasColumnName("unresolved_assertion_count");
        builder.Property(x => x.AnalysisDurationMs).HasColumnName("analysis_duration_ms");
        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at").IsRequired();

        builder.HasOne(x => x.ExperimentRun)
            .WithMany()
            .HasForeignKey(x => x.ExperimentRunId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CandidateMethod)
            .WithMany()
            .HasForeignKey(x => x.CandidateMethodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GenerationAttempt)
            .WithMany(x => x.AssertionLineageMeasurements)
            .HasForeignKey(x => x.GenerationAttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ToolAttempt)
            .WithMany(x => x.AssertionLineageMeasurements)
            .HasForeignKey(x => x.ToolAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.ExperimentRunId);
        builder.HasIndex(x => x.CandidateMethodId);
        builder.HasIndex(x => new
            {
                x.GenerationAttemptId,
                x.PolicyVersion,
                x.AssertionCatalogVersion,
                x.MaxDepth
            })
            .IsUnique()
            .HasFilter("generation_attempt_id IS NOT NULL");
        builder.HasIndex(x => new
            {
                x.ToolAttemptId,
                x.PolicyVersion,
                x.AssertionCatalogVersion,
                x.MaxDepth
            })
            .IsUnique()
            .HasFilter("tool_attempt_id IS NOT NULL");
    }
}
