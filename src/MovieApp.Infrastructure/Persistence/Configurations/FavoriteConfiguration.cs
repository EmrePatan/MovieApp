using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("favorites");

        builder.HasKey(favorite => favorite.Id);

        builder.Property(favorite => favorite.Id)
            .ValueGeneratedNever();

        builder.Property(favorite => favorite.UserId)
            .IsRequired();

        builder.Property(favorite => favorite.CreatedAt)
            .IsRequired();

        builder.HasOne(favorite => favorite.User)
            .WithMany(user => user.Favorites)
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(favorite => favorite.Movie)
            .WithMany(movie => movie.Favorites)
            .HasForeignKey(favorite => favorite.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(favorite => favorite.TvShow)
            .WithMany(tvShow => tvShow.Favorites)
            .HasForeignKey(favorite => favorite.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(favorite => favorite.UserId);

        builder.HasIndex(favorite => favorite.MovieId);

        builder.HasIndex(favorite => favorite.TvShowId);

        builder.HasIndex(favorite => new { favorite.UserId, favorite.MovieId })
            .IsUnique()
            .HasFilter("\"MovieId\" IS NOT NULL");

        builder.HasIndex(favorite => new { favorite.UserId, favorite.TvShowId })
            .IsUnique()
            .HasFilter("\"TvShowId\" IS NOT NULL");
    }
}
