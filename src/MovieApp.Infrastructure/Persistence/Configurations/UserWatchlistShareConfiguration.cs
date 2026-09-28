using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

public sealed class UserWatchlistShareConfiguration : IEntityTypeConfiguration<UserWatchlistShare>
{
    public void Configure(EntityTypeBuilder<UserWatchlistShare> builder)
    {
        builder.ToTable("user_watchlist_shares");

        builder.HasKey(share => share.Id);

        builder.Property(share => share.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(share => share.UserId);

        builder.HasIndex(share => share.WatchlistId)
            .IsUnique();

        builder.HasIndex(share => share.TokenHash)
            .IsUnique();

        builder.HasOne(share => share.User)
            .WithMany()
            .HasForeignKey(share => share.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(share => share.Watchlist)
            .WithMany()
            .HasForeignKey(share => share.WatchlistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
