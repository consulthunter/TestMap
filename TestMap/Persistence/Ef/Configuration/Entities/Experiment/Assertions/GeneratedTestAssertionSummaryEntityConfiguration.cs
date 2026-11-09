using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment.Assertions;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment.Assertions;

public sealed class GeneratedTestAssertionSummaryEntityConfiguration
    : IEntityTypeConfiguration<GeneratedTestAssertionSummaryEntity>
{
    public void Configure(EntityTypeBuilder<GeneratedTestAssertionSummaryEntity> builder)
    {
        builder.ToTable(
            "generated_test_assertion_summaries",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_generated_test_assertion_summaries_single_owner",
                    "generated_test_execution_id IS NULL OR tool_attempt_generated_test_id IS NULL");
                table.HasCheckConstraint(
                    "ck_generated_test_assertion_summaries_category_counts",
                    "recognized_assertion_count IS NULL OR " +
                    "recognized_assertion_count = traced_assertion_count + trivial_assertion_count + unresolved_assertion_count");
            });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AssertionLineageMeasurementId)
            .HasColumnName("assertion_lineage_measurement_id")
            .IsRequired();
        builder.Property(x => x.TestMemberId).HasColumnName("test_member_id");
        builder.Property(x => x.GeneratedTestExecutionId).HasColumnName("generated_test_execution_id");
        builder.Property(x => x.ToolAttemptGeneratedTestId).HasColumnName("tool_attempt_generated_test_id");
        builder.Property(x => x.TestMethodName).HasColumnName("test_method_name").IsRequired();
        builder.Property(x => x.TestFilePath).HasColumnName("test_file_path").IsRequired();
        builder.Property(x => x.TestMemberContentHash).HasColumnName("test_member_content_hash").IsRequired();
        builder.Property(x => x.FallbackIdentityHash).HasColumnName("fallback_identity_hash").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.StatusReason).HasColumnName("status_reason");
        builder.Property(x => x.RecognizedAssertionCount).HasColumnName("recognized_assertion_count");
        builder.Property(x => x.UnrecognizedAssertionCount).HasColumnName("unrecognized_assertion_count");
        builder.Property(x => x.TracedAssertionCount).HasColumnName("traced_assertion_count");
        builder.Property(x => x.TrivialAssertionCount).HasColumnName("trivial_assertion_count");
        builder.Property(x => x.UnresolvedAssertionCount).HasColumnName("unresolved_assertion_count");

        builder.HasOne(x => x.AssertionLineageMeasurement)
            .WithMany(x => x.GeneratedTestSummaries)
            .HasForeignKey(x => x.AssertionLineageMeasurementId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.GeneratedTestExecution)
            .WithMany(x => x.AssertionLineageSummaries)
            .HasForeignKey(x => x.GeneratedTestExecutionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ToolAttemptGeneratedTest)
            .WithMany(x => x.AssertionLineageSummaries)
            .HasForeignKey(x => x.ToolAttemptGeneratedTestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.AssertionLineageMeasurementId);
        builder.HasIndex(x => x.TestMemberId);
        builder.HasIndex(x => new { x.AssertionLineageMeasurementId, x.TestMemberId })
            .IsUnique()
            .HasFilter("test_member_id IS NOT NULL");
        builder.HasIndex(x => new { x.AssertionLineageMeasurementId, x.FallbackIdentityHash })
            .IsUnique()
            .HasFilter("test_member_id IS NULL");
        builder.HasIndex(x => x.GeneratedTestExecutionId)
            .IsUnique()
            .HasFilter("generated_test_execution_id IS NOT NULL");
        builder.HasIndex(x => x.ToolAttemptGeneratedTestId)
            .IsUnique()
            .HasFilter("tool_attempt_generated_test_id IS NOT NULL");
    }
}
