using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class SourceTestMappingEntityConfiguration : IEntityTypeConfiguration<SourceTestMappingEntity>
{
    public void Configure(EntityTypeBuilder<SourceTestMappingEntity> builder)
    {
        builder.ToTable("source_test_mappings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.SourceMemberId).HasColumnName("source_member_id").IsRequired();
        builder.Property(x => x.TestMemberId).HasColumnName("test_member_id").IsRequired();
        builder.Property(x => x.EvidenceKind).HasColumnName("evidence_kind").IsRequired();
        builder.Property(x => x.IsGrounded).HasColumnName("is_grounded").IsRequired();
        builder.Property(x => x.AccessPathStrategy).HasColumnName("access_path_strategy");
        builder.Property(x => x.PathLength).HasColumnName("path_length").IsRequired();
        builder.Property(x => x.Confidence).HasColumnName("confidence").IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary");
        builder.Property(x => x.ResolverVersion).HasColumnName("resolver_version").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.SourceMemberId);
        builder.HasIndex(x => x.TestMemberId);
        builder.HasIndex(x => new { x.ProjectId, x.SourceMemberId, x.TestMemberId, x.EvidenceKind })
            .IsUnique();
    }
}
