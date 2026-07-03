using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Configuration.Entities.Code;

public class CodeMetricEntityConfiguration : IEntityTypeConfiguration<CodeMetricEntity>
{
    public void Configure(EntityTypeBuilder<CodeMetricEntity> builder)
    {
        builder.ToTable("code_metrics");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(x => x.EntityType).HasColumnName("entity_type").IsRequired();
        builder.Property(x => x.MaintainabilityIndex).HasColumnName("maintainability_index").IsRequired();
        builder.Property(x => x.CyclomaticComplexity).HasColumnName("cyclomatic_complexity").IsRequired();
        builder.Property(x => x.ClassCoupling).HasColumnName("class_coupling").IsRequired();
        builder.Property(x => x.DepthOfInheritance).HasColumnName("depth_of_inheritance").IsRequired();
        builder.Property(x => x.SourceLinesOfCode).HasColumnName("source_lines_of_code").IsRequired();
        builder.Property(x => x.ExecutableLinesOfCode).HasColumnName("executable_lines_of_code").IsRequired();
    }
}