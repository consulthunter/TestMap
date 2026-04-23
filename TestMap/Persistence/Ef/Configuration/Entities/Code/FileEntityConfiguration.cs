using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Configuration.Entities.Code;

public class FileEntityConfiguration : IEntityTypeConfiguration<FileEntity>
{
    public void Configure(EntityTypeBuilder<FileEntity> builder)
    {
        builder.ToTable("files");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CSharpProjectId).HasColumnName("csharp_project_id").IsRequired();
        builder.Property(x => x.UsingStatements)
            .HasColumnName("using_statements")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .IsRequired();
        builder.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash");
    }
}