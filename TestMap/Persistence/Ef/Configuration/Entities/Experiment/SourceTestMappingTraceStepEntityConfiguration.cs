using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class SourceTestMappingTraceStepEntityConfiguration : IEntityTypeConfiguration<SourceTestMappingTraceStepEntity>
{
    public void Configure(EntityTypeBuilder<SourceTestMappingTraceStepEntity> builder)
    {
        builder.ToTable("source_test_mapping_trace_steps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SourceTestMappingId).HasColumnName("source_test_mapping_id").IsRequired();
        builder.Property(x => x.StepIndex).HasColumnName("step_index").IsRequired();
        builder.Property(x => x.FromMemberId).HasColumnName("from_member_id").IsRequired();
        builder.Property(x => x.ToMemberId).HasColumnName("to_member_id").IsRequired();
        builder.Property(x => x.RelationshipKind).HasColumnName("relationship_kind").IsRequired();
        builder.Property(x => x.EdgeSource).HasColumnName("edge_source").IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary");

        builder.HasOne(x => x.SourceTestMapping)
            .WithMany(x => x.TraceSteps)
            .HasForeignKey(x => x.SourceTestMappingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SourceTestMappingId);
        builder.HasIndex(x => new { x.SourceTestMappingId, x.StepIndex }).IsUnique();
        builder.HasIndex(x => new { x.FromMemberId, x.ToMemberId });
    }
}
