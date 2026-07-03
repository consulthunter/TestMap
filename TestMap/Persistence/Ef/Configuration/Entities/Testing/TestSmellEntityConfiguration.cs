using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Testing;

namespace TestMap.Persistence.Ef.Configuration.Entities.Testing;

public class TestSmellEntityConfiguration : IEntityTypeConfiguration<TestSmellEntity>
{
    public void Configure(EntityTypeBuilder<TestSmellEntity> builder)
    {
        builder.ToTable("test_smells");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.MemberId).HasColumnName("member_id");
        builder.Property(x => x.ObjectId).HasColumnName("object_id");
        builder.Property(x => x.SmellId).HasColumnName("smell_id").IsRequired();
        builder.Property(x => x.SmellName).HasColumnName("smell_name").IsRequired();
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();
        builder.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(x => x.Line).HasColumnName("line");
        builder.Property(x => x.Column).HasColumnName("column");
        builder.Property(x => x.ContainingTypeName).HasColumnName("containing_type_name");
        builder.Property(x => x.TestMethodName).HasColumnName("test_method_name");
        builder.Property(x => x.AnalyzedAtUtc).HasColumnName("analyzed_at_utc").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.ObjectId);
        builder.HasIndex(x => x.SmellId);
        builder.HasIndex(x => new { x.ProjectId, x.MemberId, x.SmellId, x.FilePath, x.Line, x.Column });
    }
}