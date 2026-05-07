using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Configuration.Entities.Code;

public class InvocationEntityConfiguration : IEntityTypeConfiguration<InvocationEntity>
{
    public void Configure(EntityTypeBuilder<InvocationEntity> builder)
    {
        builder.ToTable("invocations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(x => x.InvokedMemberId).HasColumnName("invoked_member_id");
        builder.Property(x => x.IsAssertion).HasColumnName("is_assertion").IsRequired();
        builder.Property(x => x.FullString).HasColumnName("full_string").IsRequired();
        builder.Property(x => x.Location)
            .HasColumnName("location")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Location>(v, (JsonSerializerOptions?)null) ?? new Location(0, 0, 0, 0))
            .IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash");
    }
}