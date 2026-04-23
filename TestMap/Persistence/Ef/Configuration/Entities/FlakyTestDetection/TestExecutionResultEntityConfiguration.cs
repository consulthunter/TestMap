using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Configuration.Entities.FlakyTestDetection;

public class TestExecutionResultEntityConfiguration : IEntityTypeConfiguration<TestExecutionResultEntity>
{
    public void Configure(EntityTypeBuilder<TestExecutionResultEntity> builder)
    {
        builder.ToTable("test_execution_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(x => x.SolutionPath).HasColumnName("solution_path").IsRequired();
        builder.Property(x => x.ProjectPath).HasColumnName("project_path").IsRequired();
        builder.Property(x => x.TestMemberId).HasColumnName("test_member_id");
        builder.Property(x => x.TestName).HasColumnName("test_name").IsRequired();
        builder.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(x => x.TargetFramework).HasColumnName("target_framework").IsRequired();
        builder.Property(x => x.ExecutionContext).HasColumnName("execution_context").IsRequired();
        builder.Property(x => x.Outcome).HasColumnName("outcome").IsRequired();
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms").IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.ErrorStackTrace).HasColumnName("error_stack_trace");
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.RunId);
        builder.HasIndex(x => x.TestMemberId);
        builder.HasIndex(x => new { x.TestName, x.FilePath });
    }
}
