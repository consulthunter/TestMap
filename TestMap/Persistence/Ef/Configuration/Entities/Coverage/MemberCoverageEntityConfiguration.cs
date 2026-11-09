using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Configuration.Entities.Coverage;

public class MemberCoverageEntityConfiguration : IEntityTypeConfiguration<MemberCoverageEntity>
{
    public void Configure(EntityTypeBuilder<MemberCoverageEntity> builder)
    {
        builder.ToTable("member_coverages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MemberId).HasColumnName("member_id");
        builder.Property(x => x.CoverageReportId).HasColumnName("coverage_report_id").IsRequired();
        builder.Property(x => x.ObjectCoverageId).HasColumnName("object_coverage_id");
        builder.Property(x => x.SourceOrdinal).HasColumnName("source_ordinal").HasDefaultValue(-1)
            .ValueGeneratedNever().IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.Signature).HasColumnName("signature").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.AttributionStatus).HasColumnName("attribution_status").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.AttributionReason).HasColumnName("attribution_reason").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.LineCountsAvailable).HasColumnName("line_counts_available").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.BranchCountsAvailable).HasColumnName("branch_counts_available").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.LineRate).HasColumnName("line_rate").IsRequired();
        builder.Property(x => x.BranchRate).HasColumnName("branch_rate").IsRequired();
        builder.Property(x => x.LinesCovered).HasColumnName("lines_covered").IsRequired();
        builder.Property(x => x.LinesValid).HasColumnName("lines_valid").IsRequired();
        builder.Property(x => x.BranchesCovered).HasColumnName("branches_covered").IsRequired();
        builder.Property(x => x.BranchesValid).HasColumnName("branches_valid").IsRequired();
        builder.Property(x => x.Complexity).HasColumnName("complexity").IsRequired();

        builder.HasOne(x => x.ObjectCoverage)
            .WithMany(x => x.MemberCoverages)
            .HasForeignKey(x => x.ObjectCoverageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ObjectCoverageId, x.SourceOrdinal })
            .IsUnique()
            .HasFilter("object_coverage_id IS NOT NULL AND source_ordinal >= 0");
    }
}
