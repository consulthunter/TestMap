using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.AgentTools;

namespace TestMap.Persistence.Ef.Configuration.Entities.AgentTools;

public class ToolAttemptEntityConfiguration : IEntityTypeConfiguration<ToolAttemptEntity>
{
    public void Configure(EntityTypeBuilder<ToolAttemptEntity> builder)
    {
        builder.ToTable("tool_attempts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id").IsRequired();
        builder.Property(x => x.MatrixWorkItemId).HasColumnName("matrix_work_item_id");
        builder.Property(x => x.CandidateMethodId).HasColumnName("candidate_method_id").IsRequired();
        builder.Property(x => x.TargetedBaselineId).HasColumnName("targeted_baseline_id");
        builder.Property(x => x.PostAttemptTestRunId).HasColumnName("post_attempt_test_run_id");
        builder.Property(x => x.EffectiveProfileHash).HasColumnName("effective_profile_hash").IsRequired();
        builder.Property(x => x.ToolId).HasColumnName("tool_id").IsRequired();
        builder.Property(x => x.RunStatus).HasColumnName("run_status").IsRequired();
        builder.Property(x => x.ValidationOutcome).HasColumnName("validation_outcome").IsRequired();
        builder.Property(x => x.ObservedOutcome).HasColumnName("observed_outcome").IsRequired();
        builder.Property(x => x.ImageName).HasColumnName("image_name").IsRequired();
        builder.Property(x => x.ImageKey).HasColumnName("image_key").IsRequired();
        builder.Property(x => x.BaseCommit).HasColumnName("base_commit").IsRequired();
        builder.Property(x => x.WorkspacePath).HasColumnName("workspace_path").IsRequired();
        builder.Property(x => x.ArtifactPath).HasColumnName("artifact_path").IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.ElapsedSeconds).HasColumnName("elapsed_seconds").IsRequired();
        builder.Property(x => x.TimeoutSeconds).HasColumnName("timeout_seconds").IsRequired();
        builder.Property(x => x.ExitCode).HasColumnName("exit_code");
        builder.Property(x => x.ToolVersion).HasColumnName("tool_version").IsRequired();
        builder.Property(x => x.Model).HasColumnName("model").IsRequired();
        builder.Property(x => x.ProviderId).HasColumnName("provider_id").IsRequired();
        builder.Property(x => x.JsonlLogAvailable).HasColumnName("jsonl_log_available").IsRequired();
        builder.Property(x => x.UsageAvailable).HasColumnName("usage_available").IsRequired();
        builder.Property(x => x.UsageSource).HasColumnName("usage_source").IsRequired();
        builder.Property(x => x.InputTokens).HasColumnName("input_tokens");
        builder.Property(x => x.OutputTokens).HasColumnName("output_tokens");
        builder.Property(x => x.EstimatedPromptTokens).HasColumnName("estimated_prompt_tokens");
        builder.Property(x => x.ChangedFilesCount).HasColumnName("changed_files_count").IsRequired();
        builder.Property(x => x.ProductionFilesChanged).HasColumnName("production_files_changed").IsRequired();
        builder.Property(x => x.TestFilesChanged).HasColumnName("test_files_changed").IsRequired();
        builder.Property(x => x.ProjectFilesChanged).HasColumnName("project_files_changed").IsRequired();
        builder.Property(x => x.DeletedFilesCount).HasColumnName("deleted_files_count").IsRequired();
        builder.Property(x => x.ConstraintViolationSummary).HasColumnName("constraint_violation_summary").IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").IsRequired();

        builder.HasOne(x => x.ExperimentRun)
            .WithMany()
            .HasForeignKey(x => x.ExperimentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MatrixWorkItem)
            .WithMany()
            .HasForeignKey(x => x.MatrixWorkItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CandidateMethod)
            .WithMany()
            .HasForeignKey(x => x.CandidateMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ExperimentRunId);
        builder.HasIndex(x => x.CandidateMethodId);
        builder.HasIndex(x => new { x.ExperimentRunId, x.ToolId });
    }
}
