using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public sealed class ExperimentMatrixWorkItemEntityConfiguration : IEntityTypeConfiguration<ExperimentMatrixWorkItemEntity>
{
    public void Configure(EntityTypeBuilder<ExperimentMatrixWorkItemEntity> builder)
    {
        builder.ToTable("experiment_matrix_work_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id").IsRequired();
        builder.Property(x => x.CandidateMethodId).HasColumnName("candidate_method_id").IsRequired();
        builder.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(x => x.StableKey).HasColumnName("stable_key").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.ProviderName).HasColumnName("provider_name").IsRequired();
        builder.Property(x => x.ModelName).HasColumnName("model_name").IsRequired();
        builder.Property(x => x.Objective).HasColumnName("objective").IsRequired();
        builder.Property(x => x.Approach).HasColumnName("approach").IsRequired();
        builder.Property(x => x.MetricsPath).HasColumnName("metrics_path").IsRequired();
        builder.Property(x => x.ContextMode).HasColumnName("context_mode").IsRequired();
        builder.Property(x => x.BudgetMode).HasColumnName("budget_mode").IsRequired();
        builder.Property(x => x.AblationVariantId).HasColumnName("ablation_variant_id").IsRequired();
        builder.Property(x => x.StepConfigJson).HasColumnName("step_config_json").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.LastHeartbeatAt).HasColumnName("last_heartbeat_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").IsRequired();

        builder.HasIndex(x => x.StableKey).IsUnique();
        builder.HasIndex(x => new { x.ExperimentRunId, x.Status });

        builder.HasOne(x => x.ExperimentRun)
            .WithMany()
            .HasForeignKey(x => x.ExperimentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CandidateMethod)
            .WithMany()
            .HasForeignKey(x => x.CandidateMethodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
