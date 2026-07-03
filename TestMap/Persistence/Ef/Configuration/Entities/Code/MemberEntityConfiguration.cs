using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Configuration.Entities.Code;

public class MemberEntityConfiguration : IEntityTypeConfiguration<MemberEntity>
{
    public void Configure(EntityTypeBuilder<MemberEntity> builder)
    {
        builder.ToTable("members");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ObjectEntityId).HasColumnName("object_id").IsRequired();
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
        builder.Property(x => x.IsTestMember).HasColumnName("is_test_member").IsRequired();
        builder.Property(x => x.IsGenerated).HasColumnName("is_generated").IsRequired();
        builder.Property(x => x.TestCategories)
            .HasColumnName("test_categories")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .IsRequired();
        builder.Property(x => x.TestIntent).HasColumnName("test_intent");
        builder.Property(x => x.TestMetadataSource).HasColumnName("test_metadata_source");
        builder.Property(x => x.TestMetadataConfidence).HasColumnName("test_metadata_confidence");
        builder.Property(x => x.TestMetadataPromptVersion).HasColumnName("test_metadata_prompt_version");
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