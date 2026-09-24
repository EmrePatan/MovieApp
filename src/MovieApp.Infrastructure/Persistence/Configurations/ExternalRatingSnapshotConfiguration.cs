using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class ExternalRatingSnapshotConfiguration : IEntityTypeConfiguration<ExternalRatingSnapshot>
{
    public void Configure(EntityTypeBuilder<ExternalRatingSnapshot> builder)
    {
        builder.ToTable("external_rating_snapshots");

        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Id)
            .ValueGeneratedNever();

        builder.Property(snapshot => snapshot.MediaType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(snapshot => snapshot.TmdbId)
            .IsRequired();

        builder.Property(snapshot => snapshot.Provider)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(snapshot => snapshot.PayloadJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(snapshot => snapshot.FetchedAtUtc)
            .IsRequired();

        builder.HasIndex(snapshot => new { snapshot.MediaType, snapshot.TmdbId, snapshot.Provider })
            .IsUnique();
    }
}
