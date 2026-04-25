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
        builder.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(x => x.CoverageReportId).HasColumnName("coverage_report_id").IsRequired();
        builder.Property(x => x.LineRate).HasColumnName("line_rate").IsRequired();
        builder.Property(x => x.BranchRate).HasColumnName("branch_rate").IsRequired();
        builder.Property(x => x.LinesCovered).HasColumnName("lines_covered").IsRequired();
        builder.Property(x => x.LinesValid).HasColumnName("lines_valid").IsRequired();
        builder.Property(x => x.BranchesCovered).HasColumnName("branches_covered").IsRequired();
        builder.Property(x => x.BranchesValid).HasColumnName("branches_valid").IsRequired();
        builder.Property(x => x.Complexity).HasColumnName("complexity").IsRequired();
    }
}