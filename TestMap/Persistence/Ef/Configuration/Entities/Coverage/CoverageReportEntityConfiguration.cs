using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Configuration.Entities.Coverage;

public class CoverageReportEntityConfiguration : IEntityTypeConfiguration<CoverageReportEntity>
{
    public void Configure(EntityTypeBuilder<CoverageReportEntity> builder)
    {
        builder.ToTable("coverage_reports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.LineRate).HasColumnName("line_rate").IsRequired();
        builder.Property(x => x.BranchRate).HasColumnName("branch_rate").IsRequired();
        builder.Property(x => x.Complexity).HasColumnName("complexity").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.Timestamp).HasColumnName("timestamp").IsRequired();
        builder.Property(x => x.LinesCovered).HasColumnName("lines_covered").IsRequired();
        builder.Property(x => x.LinesValid).HasColumnName("lines_valid").IsRequired();
        builder.Property(x => x.BranchesCovered).HasColumnName("branches_covered").IsRequired();
        builder.Property(x => x.BranchesValid).HasColumnName("branches_valid").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}
