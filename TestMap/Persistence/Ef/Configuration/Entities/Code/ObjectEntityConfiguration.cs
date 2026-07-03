using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Configuration.Entities.Code;

public class ObjectEntityConfiguration : IEntityTypeConfiguration<ObjectEntity>
{
    public void Configure(EntityTypeBuilder<ObjectEntity> builder)
    {
        builder.ToTable("objects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FileId).HasColumnName("file_id").IsRequired();
        builder.Property(x => x.Namespace).HasColumnName("namespace").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").IsRequired();
        builder.Property(x => x.Modifiers)
            .HasColumnName("modifiers")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .IsRequired();
        builder.Property(x => x.Attributes)
            .HasColumnName("attributes")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .IsRequired();
        builder.Property(x => x.DocString).HasColumnName("doc_string");
        builder.Property(x => x.FullString).HasColumnName("full_string");
        builder.Property(x => x.IsTestObject).HasColumnName("is_test_object").IsRequired();
        builder.Property(x => x.TestFramework).HasColumnName("test_framework");
        builder.OwnsOne(x => x.Location, location =>
        {
            location.Property(x => x.StartLineNumber)
                .HasColumnName("start_line_number")
                .IsRequired();

            location.Property(x => x.BodyStartPosition)
                .HasColumnName("body_start_position")
                .IsRequired();

            location.Property(x => x.EndLineNumber)
                .HasColumnName("end_line_number")
                .IsRequired();

            location.Property(x => x.BodyEndPosition)
                .HasColumnName("body_end_position")
                .IsRequired();
        });
        builder.Property(x => x.ContentHash).HasColumnName("content_hash");
    }
}