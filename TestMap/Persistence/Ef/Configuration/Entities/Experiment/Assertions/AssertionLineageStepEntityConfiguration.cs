using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment.Assertions;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment.Assertions;

public sealed class AssertionLineageStepEntityConfiguration
    : IEntityTypeConfiguration<AssertionLineageStepEntity>
{
    public void Configure(EntityTypeBuilder<AssertionLineageStepEntity> builder)
    {
        builder.ToTable("assertion_lineage_steps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AssertionObservationId).HasColumnName("assertion_observation_id").IsRequired();
        builder.Property(x => x.InputIndex).HasColumnName("input_index").IsRequired();
        builder.Property(x => x.PathIndex).HasColumnName("path_index").IsRequired();
        builder.Property(x => x.StepIndex).HasColumnName("step_index").IsRequired();
        builder.Property(x => x.StepKind).HasColumnName("step_kind").IsRequired();
        builder.Property(x => x.Depth).HasColumnName("depth").IsRequired();
        builder.Property(x => x.SymbolDisplay).HasColumnName("symbol_display").IsRequired();
        builder.Property(x => x.MemberId).HasColumnName("member_id");
        builder.Property(x => x.FilePath).HasColumnName("file_path");
        builder.Property(x => x.StartLine).HasColumnName("start_line");
        builder.Property(x => x.StartColumn).HasColumnName("start_column");
        builder.Property(x => x.EndLine).HasColumnName("end_line");
        builder.Property(x => x.EndColumn).HasColumnName("end_column");
        builder.Property(x => x.Outcome).HasColumnName("outcome").IsRequired();
        builder.Property(x => x.ReasonCode).HasColumnName("reason_code").IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary").IsRequired();

        builder.HasOne(x => x.AssertionObservation)
            .WithMany(x => x.LineageSteps)
            .HasForeignKey(x => x.AssertionObservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new
        {
            x.AssertionObservationId,
            x.InputIndex,
            x.PathIndex,
            x.StepIndex
        }).IsUnique();
        builder.HasIndex(x => new { x.StepKind, x.MemberId });
    }
}
