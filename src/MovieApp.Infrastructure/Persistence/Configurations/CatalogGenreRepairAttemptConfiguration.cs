using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class CatalogGenreRepairAttemptConfiguration : IEntityTypeConfiguration<CatalogGenreRepairAttempt>
{
    public void Configure(EntityTypeBuilder<CatalogGenreRepairAttempt> builder)
    {
        builder.ToTable("catalog_genre_repair_attempts");

        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.Id)
            .ValueGeneratedNever();

        builder.Property(attempt => attempt.CatalogId)
            .IsRequired();

        builder.Property(attempt => attempt.ContentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(8);

        builder.Property(attempt => attempt.LastAttemptAtUtc)
            .IsRequired();

        builder.Property(attempt => attempt.LastOutcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(attempt => attempt.NextEligibleAtUtc)
            .IsRequired();

        builder.HasIndex(attempt => new { attempt.CatalogId, attempt.ContentType })
            .IsUnique();

        builder.HasIndex(attempt => new { attempt.ContentType, attempt.NextEligibleAtUtc });
    }
}
