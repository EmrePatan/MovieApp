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
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(releaseEvent => releaseEvent.Movie)
            .WithMany(movie => movie.CatalogReleaseEvents)
            .HasForeignKey(releaseEvent => releaseEvent.MovieId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(releaseEvent => releaseEvent.TvShowId);

        builder.HasIndex(releaseEvent => releaseEvent.MovieId);

        builder.HasIndex(releaseEvent => releaseEvent.ReleaseAtUtc);

        builder.HasIndex(releaseEvent => releaseEvent.EventType);

        builder.HasIndex(releaseEvent => releaseEvent.DedupeKey)
            .IsUnique();

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "CK_catalog_release_events_content_ref",
            "(\"TvShowId\" IS NOT NULL AND \"MovieId\" IS NULL) OR (\"TvShowId\" IS NULL AND \"MovieId\" IS NOT NULL)"));
    }
}
