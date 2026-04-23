using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Configuration.Entities.Coverage;

public class CoverageGapEntityConfiguration : IEntityTypeConfiguration<CoverageGapEntity>
{
    public void Configure(EntityTypeBuilder<CoverageGapEntity> builder)
    {
        builder.ToTable("coverage_gaps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(x => x.CoverageReportId).HasColumnName("coverage_report_id").IsRequired();
        builder.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
        builder.Property(x => x.Hits).HasColumnName("hits").IsRequired();
        builder.Property(x => x.IsBranch).HasColumnName("is_branch").IsRequired();
        builder.Property(x => x.ConditionCoverage).HasColumnName("condition_coverage").IsRequired();
        builder.Property(x => x.GapKind).HasColumnName("gap_kind").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceText).HasColumnName("source_text").IsRequired();
        builder.Property(x => x.MemberContentHash).HasColumnName("member_content_hash").HasMaxLength(128).IsRequired();

        builder.HasIndex(x => new { x.CoverageReportId, x.MemberId, x.LineNumber, x.GapKind })
            .IsUnique();
    }
}
