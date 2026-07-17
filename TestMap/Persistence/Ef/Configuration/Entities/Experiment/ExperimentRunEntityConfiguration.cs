using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class ExperimentRunEntityConfiguration : IEntityTypeConfiguration<ExperimentRunEntity>
{
    public void Configure(EntityTypeBuilder<ExperimentRunEntity> builder)
    {
        builder.ToTable("experiment_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RunUid).HasColumnName("run_uid").IsRequired();
        builder.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.Objective).HasColumnName("objective").IsRequired();
        builder.Property(x => x.CandidateSelectionStrategy).HasColumnName("candidate_selection_strategy").IsRequired();
        builder.Property(x => x.Configuration).HasColumnName("configuration").IsRequired();
        builder.Property(x => x.ResultsFilePath).HasColumnName("results_file_path").IsRequired();
        builder.Property(x => x.CandidateLimit).HasColumnName("candidate_limit").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.ExperimentSeriesId).HasColumnName("experiment_series_id").IsRequired();
        builder.Property(x => x.CandidateCohortId).HasColumnName("candidate_cohort_id");
        builder.Property(x => x.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(x => x.RepositoryIdentity).HasColumnName("repository_identity").IsRequired();
        builder.Property(x => x.RequestedCommit).HasColumnName("requested_commit").IsRequired();
        builder.Property(x => x.ResolvedCommit).HasColumnName("resolved_commit").IsRequired();
        builder.Property(x => x.TargetManifestSha256).HasColumnName("target_manifest_sha256").IsRequired();
        builder.Property(x => x.TargetSourceSha256).HasColumnName("target_source_sha256").IsRequired();
        builder.Property(x => x.MaterializedAtUtc).HasColumnName("materialized_at_utc");
        builder.Property(x => x.WorkspaceIntegrityStatus).HasColumnName("workspace_integrity_status").IsRequired();
        builder.Property(x => x.ProvenancePolicyVersion).HasColumnName("provenance_policy_version").IsRequired();

        builder.HasMany(x => x.CandidateMethods)
            .WithOne(x => x.ExperimentRun)
            .HasForeignKey(x => x.ExperimentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CandidateCohort)
            .WithMany(x => x.ExperimentRuns)
            .HasForeignKey(x => x.CandidateCohortId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ExperimentSeriesId);
        builder.HasIndex(x => x.RunUid).IsUnique();
        builder.HasIndex(x => x.CandidateCohortId);
        builder.HasIndex(x => new { x.TargetId, x.ResolvedCommit });
    }
}
