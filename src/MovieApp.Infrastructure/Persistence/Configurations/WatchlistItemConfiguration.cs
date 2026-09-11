using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> builder)
    {
        builder.ToTable("watchlist_items");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .ValueGeneratedNever();

        builder.Property(item => item.WatchlistId)
            .IsRequired();

        builder.Property(item => item.CreatedAt)
            .IsRequired();

        builder.HasOne(item => item.Watchlist)
            .WithMany(watchlist => watchlist.Items)
            .HasForeignKey(item => item.WatchlistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Movie)
            .WithMany(movie => movie.WatchlistItems)
            .HasForeignKey(item => item.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.TvShow)
            .WithMany(tvShow => tvShow.WatchlistItems)
            .HasForeignKey(item => item.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => item.WatchlistId);

        builder.HasIndex(item => item.MovieId);

        builder.HasIndex(item => item.TvShowId);

        builder.HasIndex(item => new { item.WatchlistId, item.MovieId })
            .IsUnique()
            .HasFilter("\"MovieId\" IS NOT NULL");

        builder.HasIndex(item => new { item.WatchlistId, item.TvShowId })
            .IsUnique()
            .HasFilter("\"TvShowId\" IS NOT NULL");
    }
}
