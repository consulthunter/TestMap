using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment.Assertions;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment.Assertions;

public sealed class AssertionObservationEntityConfiguration
    : IEntityTypeConfiguration<AssertionObservationEntity>
{
    public void Configure(EntityTypeBuilder<AssertionObservationEntity> builder)
    {
        builder.ToTable("assertion_observations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.GeneratedTestAssertionSummaryId)
            .HasColumnName("generated_test_assertion_summary_id")
            .IsRequired();
        builder.Property(x => x.InvocationId).HasColumnName("invocation_id");
        builder.Property(x => x.Ordinal).HasColumnName("ordinal").IsRequired();
        builder.Property(x => x.Framework).HasColumnName("framework").IsRequired();
        builder.Property(x => x.AssertionMethod).HasColumnName("assertion_method").IsRequired();
        builder.Property(x => x.RecognitionKind).HasColumnName("recognition_kind").IsRequired();
        builder.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(x => x.StartLine).HasColumnName("start_line").IsRequired();
        builder.Property(x => x.StartColumn).HasColumnName("start_column").IsRequired();
        builder.Property(x => x.EndLine).HasColumnName("end_line").IsRequired();
        builder.Property(x => x.EndColumn).HasColumnName("end_column").IsRequired();
        builder.Property(x => x.ExpressionHash).HasColumnName("expression_hash").IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").IsRequired();
        builder.Property(x => x.ResolutionCode).HasColumnName("resolution_code").IsRequired();
        builder.Property(x => x.DepthReached).HasColumnName("depth_reached").IsRequired();
        builder.Property(x => x.TargetRelation).HasColumnName("target_relation").IsRequired();
        builder.Property(x => x.TraceSummary).HasColumnName("trace_summary").IsRequired();

        builder.HasOne(x => x.GeneratedTestAssertionSummary)
            .WithMany(x => x.AssertionObservations)
            .HasForeignKey(x => x.GeneratedTestAssertionSummaryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.InvocationId);
        builder.HasIndex(x => new { x.GeneratedTestAssertionSummaryId, x.Ordinal }).IsUnique();
        builder.HasIndex(x => new { x.GeneratedTestAssertionSummaryId, x.ExpressionHash }).IsUnique();
        builder.HasIndex(x => x.Category);
    }
}
