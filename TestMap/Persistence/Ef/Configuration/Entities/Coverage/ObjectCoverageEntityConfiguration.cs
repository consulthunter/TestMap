using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Configuration.Entities.Coverage;

public class ObjectCoverageEntityConfiguration : IEntityTypeConfiguration<ObjectCoverageEntity>
{
    public void Configure(EntityTypeBuilder<ObjectCoverageEntity> builder)
    {
        builder.ToTable("object_coverages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ObjectId).HasColumnName("object_id");
        builder.Property(x => x.CoverageReportId).HasColumnName("coverage_report_id").IsRequired();
        builder.Property(x => x.SourceOrdinal).HasColumnName("source_ordinal").HasDefaultValue(-1)
            .ValueGeneratedNever().IsRequired();
        builder.Property(x => x.PackageName).HasColumnName("package_name").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.Filename).HasColumnName("filename").HasDefaultValue(string.Empty).IsRequired();
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

        builder.HasIndex(x => new { x.CoverageReportId, x.SourceOrdinal })
            .IsUnique()
            .HasFilter("source_ordinal >= 0");
    }
}
