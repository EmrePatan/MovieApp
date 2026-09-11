using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TvShowGenreConfiguration : IEntityTypeConfiguration<TvShowGenre>
{
    public void Configure(EntityTypeBuilder<TvShowGenre> builder)
    {
        builder.ToTable("tv_show_genres");

        builder.HasKey(tvShowGenre => new { tvShowGenre.TvShowId, tvShowGenre.GenreId });

        builder.HasOne(tvShowGenre => tvShowGenre.TvShow)
            .WithMany(tvShow => tvShow.TvShowGenres)
            .HasForeignKey(tvShowGenre => tvShowGenre.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tvShowGenre => tvShowGenre.Genre)
            .WithMany(genre => genre.TvShowGenres)
            .HasForeignKey(tvShowGenre => tvShowGenre.GenreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tvShowGenre => tvShowGenre.GenreId);
    }
}
