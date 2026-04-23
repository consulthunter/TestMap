using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class CandidateMethodEntityConfiguration : IEntityTypeConfiguration<CandidateMethodEntity>
{
    public void Configure(EntityTypeBuilder<CandidateMethodEntity> builder)
    {
        builder.ToTable("candidate_methods");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id").IsRequired();
        builder.Property(x => x.SourceMemberId).HasColumnName("source_member_id").IsRequired();
        builder.Property(x => x.ExistingTestMemberId).HasColumnName("existing_test_member_id");
        builder.Property(x => x.SourceMethodName).HasColumnName("source_method_name").IsRequired();
        builder.Property(x => x.SourceMethodSignature).HasColumnName("source_method_signature").IsRequired();
        builder.Property(x => x.ExistingTestMethodName).HasColumnName("existing_test_method_name");
        builder.Property(x => x.InitialCoverage).HasColumnName("initial_coverage").IsRequired();
        builder.Property(x => x.InitialCoveredLines).HasColumnName("initial_covered_lines").IsRequired();
        builder.Property(x => x.InitialTotalLines).HasColumnName("initial_total_lines").IsRequired();
        builder.Property(x => x.SelectionTime).HasColumnName("selection_time").IsRequired();

        builder.HasMany(x => x.GenerationAttempts)
            .WithOne(x => x.CandidateMethod)
            .HasForeignKey(x => x.CandidateMethodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
