using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Rules;

namespace TestMap.Persistence.Ef.Configuration.Entities.Rules;

public class RuleDefinitionEntityConfiguration : IEntityTypeConfiguration<RuleDefinitionEntity>
{
    public void Configure(EntityTypeBuilder<RuleDefinitionEntity> builder)
    {
        builder.ToTable("rule_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RuleId).HasColumnName("rule_id").IsRequired();
        builder.Property(x => x.RuleVersion).HasColumnName("rule_version").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").IsRequired();

        builder.HasIndex(x => new { x.RuleId, x.RuleVersion }).IsUnique();
        builder.HasIndex(x => x.Category);
    }
}
