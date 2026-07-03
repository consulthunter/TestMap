using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Configuration.Entities.Code;

public class CSharpSolutionEntityConfiguration : IEntityTypeConfiguration<CSharpSolutionEntity>
{
    public void Configure(EntityTypeBuilder<CSharpSolutionEntity> builder)
    {
        builder.ToTable("csharp_solutions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash");
    }
}