using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.MutationTesting;

namespace TestMap.Persistence.Ef.Configuration.Entities.MutationTesting;

public class MutantEntityConfiguration : IEntityTypeConfiguration<MutantEntity>
{
    public void Configure(EntityTypeBuilder<MutantEntity> builder)
    {
        builder.ToTable("mutants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MutationTestingReportId).HasColumnName("mutation_testing_report_id").IsRequired();
        builder.Property(x => x.MemberId).HasColumnName("member_id");
        builder.Property(x => x.StrykerMutantId).HasColumnName("stryker_mutant_id").IsRequired();
        builder.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(x => x.MutatorName).HasColumnName("mutator_name").IsRequired();
        builder.Property(x => x.Replacement).HasColumnName("replacement").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.StatusReason).HasColumnName("status_reason").IsRequired();
        builder.Property(x => x.IsStatic).HasColumnName("is_static").IsRequired();
        builder.Property(x => x.Location)
            .HasColumnName("location")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Location>(v, (JsonSerializerOptions?)null) ?? new Location(0, 0, 0, 0))
            .IsRequired();
        builder.Property(x => x.CoveredBy)
            .HasColumnName("covered_by")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .IsRequired();
        builder.Property(x => x.KilledBy)
            .HasColumnName("killed_by")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.ContentHash).IsUnique();
    }
}
