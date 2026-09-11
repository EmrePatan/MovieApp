using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Watchlists;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class WatchlistConfiguration : IEntityTypeConfiguration<Watchlist>
{
    public void Configure(EntityTypeBuilder<Watchlist> builder)
    {
        builder.ToTable("watchlists");

        builder.HasKey(watchlist => watchlist.Id);

        builder.Property(watchlist => watchlist.Id)
            .ValueGeneratedNever();

        builder.Property(watchlist => watchlist.UserId)
            .IsRequired();

        builder.Property(watchlist => watchlist.Name)
            .IsRequired()
            .HasMaxLength(WatchlistNameNormalizer.MaxLength);

        builder.Property(watchlist => watchlist.NormalizedName)
            .IsRequired()
            .HasMaxLength(WatchlistNameNormalizer.MaxLength);

        builder.Property(watchlist => watchlist.CreatedAt)
            .IsRequired();

        builder.Property(watchlist => watchlist.UpdatedAt)
            .IsRequired();

        builder.HasOne(watchlist => watchlist.User)
            .WithMany(user => user.Watchlists)
            .HasForeignKey(watchlist => watchlist.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(watchlist => watchlist.UserId);

        builder.HasIndex(watchlist => new { watchlist.UserId, watchlist.NormalizedName })
            .IsUnique();
    }
}
