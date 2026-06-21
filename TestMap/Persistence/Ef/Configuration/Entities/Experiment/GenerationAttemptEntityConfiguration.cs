using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class GenerationAttemptEntityConfiguration : IEntityTypeConfiguration<GenerationAttemptEntity>
{
    public void Configure(EntityTypeBuilder<GenerationAttemptEntity> builder)
    {
        builder.ToTable("generation_attempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CandidateMethodId).HasColumnName("candidate_method_id").IsRequired();
        builder.Property(x => x.ExperimentMatrixWorkItemId).HasColumnName("experiment_matrix_work_item_id");
        builder.Property(x => x.ProviderName).HasColumnName("provider_name").IsRequired();
        builder.Property(x => x.ModelName).HasColumnName("model_name").IsRequired();
        builder.Property(x => x.Strategy).HasColumnName("strategy").IsRequired();
        builder.Property(x => x.Objective).HasColumnName("objective").IsRequired();
        builder.Property(x => x.GenerationApproach).HasColumnName("generation_approach").IsRequired();
        builder.Property(x => x.MetricsPath).HasColumnName("metrics_path").IsRequired();
        builder.Property(x => x.ContextMode).HasColumnName("context_mode").IsRequired();
        builder.Property(x => x.BudgetMode).HasColumnName("budget_mode").IsRequired();
        builder.Property(x => x.AblationVariantId).HasColumnName("ablation_variant_id").IsRequired();
        builder.Property(x => x.StepConfigJson).HasColumnName("step_config_json").IsRequired();
        builder.Property(x => x.EffectiveProfileJson).HasColumnName("effective_profile_json").IsRequired();
        builder.Property(x => x.EffectiveProfileHash).HasColumnName("effective_profile_hash").IsRequired();
        builder.Property(x => x.Temperature).HasColumnName("temperature").IsRequired();
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(x => x.IsRepairAttempt).HasColumnName("is_repair_attempt").IsRequired();
        builder.Property(x => x.ParentAttemptId).HasColumnName("parent_attempt_id");
        builder.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.TotalTokensUsed).HasColumnName("total_tokens_used").IsRequired();
        builder.Property(x => x.GenerationDurationSeconds).HasColumnName("generation_duration_seconds").IsRequired();
        builder.Property(x => x.ValidationDurationSeconds).HasColumnName("validation_duration_seconds").IsRequired();
        builder.Property(x => x.TotalAttemptDurationSeconds).HasColumnName("total_attempt_duration_seconds").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.FailureKind).HasColumnName("failure_kind").IsRequired();
        builder.Property(x => x.FailureStage).HasColumnName("failure_stage").IsRequired();
        builder.Property(x => x.FailureCategory).HasColumnName("failure_category").IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").IsRequired();
        builder.Property(x => x.RuleDecisionSnapshotJson).HasColumnName("rule_decision_snapshot_json").IsRequired();

        // Basic Extension patch metadata — nullable so existing rows are unaffected
        builder.Property(x => x.PatchJson).HasColumnName("patch_json");
        builder.Property(x => x.RepairPatchJson).HasColumnName("repair_patch_json");
        builder.Property(x => x.PatchApplicationOutcome).HasColumnName("patch_application_outcome").HasMaxLength(100);
        builder.Property(x => x.AppliedUsingCount).HasColumnName("applied_using_count");
        builder.Property(x => x.AppliedHelperCount).HasColumnName("applied_helper_count");
        builder.Property(x => x.ModifiedFilePath).HasColumnName("modified_file_path");
        builder.Property(x => x.ModifiedFileContents).HasColumnName("modified_file_contents");
        builder.Property(x => x.ModifiedFileSha256).HasColumnName("modified_file_sha256").HasMaxLength(64);

        builder.HasOne(x => x.ParentAttempt)
            .WithMany()
            .HasForeignKey(x => x.ParentAttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ExperimentMatrixWorkItem)
            .WithMany(x => x.GenerationAttempts)
            .HasForeignKey(x => x.ExperimentMatrixWorkItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.GenerationSteps)
            .WithOne(x => x.GenerationAttempt)
            .HasForeignKey(x => x.GenerationAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TestExecution)
            .WithOne(x => x.GenerationAttempt)
            .HasForeignKey<GeneratedTestExecutionEntity>(x => x.GenerationAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ExperimentMatrixWorkItemId);
    }
}
