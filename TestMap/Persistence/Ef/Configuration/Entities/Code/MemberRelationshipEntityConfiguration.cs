using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Configuration.Entities.Code;

public class MemberRelationshipEntityConfiguration : IEntityTypeConfiguration<MemberRelationshipEntity>
{
    public void Configure(EntityTypeBuilder<MemberRelationshipEntity> builder)
    {
        builder.ToTable("MemberRelationships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SourceId).HasColumnName("SourceId").IsRequired();
        builder.Property(x => x.TargetId).HasColumnName("TargetId").IsRequired();
        builder.Property(x => x.RelationshipType).HasColumnName("RelationshipType").IsRequired();
    }
}