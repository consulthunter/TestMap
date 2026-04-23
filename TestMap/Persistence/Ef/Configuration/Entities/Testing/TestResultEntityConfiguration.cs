using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Testing;

namespace TestMap.Persistence.Ef.Configuration.Entities.Testing;

public class TestResultEntityConfiguration : IEntityTypeConfiguration<TestResultEntity>
{
    public void Configure(EntityTypeBuilder<TestResultEntity> builder)
    {
        builder.ToTable("test_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TestRunId).HasColumnName("test_run_id").IsRequired();
        builder.Property(x => x.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(x => x.RunDate).HasColumnName("run_date").IsRequired();
        builder.Property(x => x.MethodId).HasColumnName("method_id").IsRequired();
        builder.Property(x => x.TestName).HasColumnName("test_name").IsRequired();
        builder.Property(x => x.Outcome).HasColumnName("outcome").IsRequired();
        builder.Property(x => x.Duration).HasColumnName("duration").IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.StackTrace).HasColumnName("stack_trace");
    }
}
