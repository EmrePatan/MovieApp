using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class CatalogReleaseEventConfiguration : IEntityTypeConfiguration<CatalogReleaseEvent>
{
    public void Configure(EntityTypeBuilder<CatalogReleaseEvent> builder)
    {
        builder.ToTable("catalog_release_events");

        builder.HasKey(releaseEvent => releaseEvent.Id);

        builder.Property(releaseEvent => releaseEvent.Id)
            .ValueGeneratedNever();

        builder.Property(releaseEvent => releaseEvent.TvShowId)
            .IsRequired();

        builder.Property(releaseEvent => releaseEvent.EventType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(releaseEvent => releaseEvent.SeasonNumber)
            .IsRequired();

        builder.Property(releaseEvent => releaseEvent.ReleaseAtUtc)
            .IsRequired();

        builder.Property(releaseEvent => releaseEvent.DetectedAtUtc)
            .IsRequired();

        builder.Property(releaseEvent => releaseEvent.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(releaseEvent => releaseEvent.DedupeKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(releaseEvent => releaseEvent.TvShow)
            .WithMany(tvShow => tvShow.CatalogReleaseEvents)
            .HasForeignKey(releaseEvent => releaseEvent.TvShowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(releaseEvent => releaseEvent.TvShowId);

        builder.HasIndex(releaseEvent => releaseEvent.ReleaseAtUtc);

        builder.HasIndex(releaseEvent => releaseEvent.EventType);

        builder.HasIndex(releaseEvent => releaseEvent.DedupeKey)
            .IsUnique();
    }
}
