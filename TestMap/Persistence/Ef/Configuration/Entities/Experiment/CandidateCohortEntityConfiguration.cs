using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public sealed class CandidateCohortEntityConfiguration : IEntityTypeConfiguration<CandidateCohortEntity>
{
    public void Configure(EntityTypeBuilder<CandidateCohortEntity> builder)
    {
        builder.ToTable("candidate_cohorts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.CohortKey).HasColumnName("cohort_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RepositoryIdentity).HasColumnName("repository_identity").HasMaxLength(500).IsRequired();
        builder.Property(x => x.CommitHash).HasColumnName("commit_hash").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Objective).HasColumnName("objective").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SelectionStrategy).HasColumnName("selection_strategy").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SelectionConfigurationHash).HasColumnName("selection_configuration_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CandidateLimit).HasColumnName("candidate_limit").IsRequired();
        builder.Property(x => x.RandomSeed).HasColumnName("random_seed");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.CohortKey }).IsUnique();
    }
}

public sealed class CandidateCohortMemberEntityConfiguration : IEntityTypeConfiguration<CandidateCohortMemberEntity>
{
    public void Configure(EntityTypeBuilder<CandidateCohortMemberEntity> builder)
    {
        builder.ToTable("candidate_cohort_members");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CandidateCohortId).HasColumnName("candidate_cohort_id").IsRequired();
        builder.Property(x => x.Ordinal).HasColumnName("ordinal").IsRequired();
        builder.Property(x => x.SourceMemberId).HasColumnName("source_member_id").IsRequired();
        builder.Property(x => x.SourceMethodName).HasColumnName("source_method_name").HasMaxLength(500).IsRequired();
        builder.Property(x => x.SourceMethodSignature).HasColumnName("source_method_signature").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.SourceFilePath).HasColumnName("source_file_path").IsRequired();
        builder.Property(x => x.ContainingType).HasColumnName("containing_type").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SourceContentHash).HasColumnName("source_content_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.CandidateSnapshotJson).HasColumnName("candidate_snapshot_json").IsRequired();

        builder.HasOne(x => x.CandidateCohort)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.CandidateCohortId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CandidateCohortId, x.Ordinal }).IsUnique();
        builder.HasIndex(x => new { x.CandidateCohortId, x.SourceMemberId }).IsUnique();
    }
}
