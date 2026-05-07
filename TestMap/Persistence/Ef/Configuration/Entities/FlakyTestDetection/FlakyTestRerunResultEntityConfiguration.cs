using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Configuration.Entities.FlakyTestDetection;

public class FlakyTestRerunResultEntityConfiguration : IEntityTypeConfiguration<FlakyTestRerunResultEntity>
{
    public void Configure(EntityTypeBuilder<FlakyTestRerunResultEntity> builder)
    {
        builder.ToTable("flaky_test_rerun_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(x => x.TestExecutionResultId).HasColumnName("test_execution_result_id").IsRequired();
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(x => x.Outcome).HasColumnName("outcome").IsRequired();
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms").IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.RunId);
        builder.HasIndex(x => x.TestExecutionResultId);
    }
}