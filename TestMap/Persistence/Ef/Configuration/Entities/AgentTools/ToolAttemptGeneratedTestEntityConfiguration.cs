using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.AgentTools;

namespace TestMap.Persistence.Ef.Configuration.Entities.AgentTools;

public class ToolAttemptGeneratedTestEntityConfiguration
    : IEntityTypeConfiguration<ToolAttemptGeneratedTestEntity>
{
    public void Configure(EntityTypeBuilder<ToolAttemptGeneratedTestEntity> builder)
    {
        builder.ToTable("tool_attempt_generated_tests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ToolAttemptId).HasColumnName("tool_attempt_id").IsRequired();
        builder.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(x => x.MappingId).HasColumnName("mapping_id");

        builder.HasOne(x => x.ToolAttempt)
            .WithMany()
            .HasForeignKey(x => x.ToolAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ToolAttemptId);
        builder.HasIndex(x => x.MemberId);
    }
}
