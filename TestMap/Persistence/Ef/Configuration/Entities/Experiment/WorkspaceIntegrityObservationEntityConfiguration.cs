using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public sealed class WorkspaceIntegrityObservationEntityConfiguration : IEntityTypeConfiguration<WorkspaceIntegrityObservationEntity>
{
    public void Configure(EntityTypeBuilder<WorkspaceIntegrityObservationEntity> builder)
    {
        builder.ToTable("workspace_integrity_observations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id");
        builder.Property(x => x.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(x => x.ProducerLane).HasColumnName("producer_lane");
        builder.Property(x => x.WorkItemStableKey).HasColumnName("work_item_stable_key");
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number");
        builder.Property(x => x.Checkpoint).HasColumnName("checkpoint").IsRequired();
        builder.Property(x => x.ExpectedCommit).HasColumnName("expected_commit").IsRequired();
        builder.Property(x => x.ActualCommit).HasColumnName("actual_commit");
        builder.Property(x => x.OriginMatches).HasColumnName("origin_matches").IsRequired();
        builder.Property(x => x.WorkingTreeDirty).HasColumnName("working_tree_dirty");
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.Details).HasColumnName("details").IsRequired();
        builder.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.HasIndex(x => new { x.ExperimentRunId, x.Checkpoint, x.Status });
        builder.HasIndex(x => new { x.TargetId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.WorkItemStableKey, x.AttemptNumber, x.Checkpoint });
    }
}
