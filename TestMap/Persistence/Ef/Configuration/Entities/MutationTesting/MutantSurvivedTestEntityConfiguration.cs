using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.MutationTesting;

namespace TestMap.Persistence.Ef.Configuration.Entities.MutationTesting;

public class MutantSurvivedTestEntityConfiguration : IEntityTypeConfiguration<MutantSurvivedTestEntity>
{
    public void Configure(EntityTypeBuilder<MutantSurvivedTestEntity> builder)
    {
        builder.ToTable("mutant_survived_tests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MutantId).HasColumnName("mutant_id").IsRequired();
        builder.Property(x => x.TestMemberId).HasColumnName("test_member_id");
        builder.Property(x => x.StrykerTestId).HasColumnName("stryker_test_id").IsRequired();
        builder.Property(x => x.TestName).HasColumnName("test_name").IsRequired();
        builder.Property(x => x.TestFilePath).HasColumnName("test_file_path").IsRequired();
        builder.Property(x => x.Location)
            .HasColumnName("location")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Location>(v, (JsonSerializerOptions?)null) ?? new Location(0, 0, 0, 0))
            .IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").IsRequired();

        builder.HasIndex(x => x.ContentHash).IsUnique();
    }
}