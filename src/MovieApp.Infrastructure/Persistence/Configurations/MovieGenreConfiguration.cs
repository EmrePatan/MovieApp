using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class MovieGenreConfiguration : IEntityTypeConfiguration<MovieGenre>
{
    public void Configure(EntityTypeBuilder<MovieGenre> builder)
    {
        builder.ToTable("movie_genres");

        builder.HasKey(movieGenre => new { movieGenre.MovieId, movieGenre.GenreId });

        builder.HasOne(movieGenre => movieGenre.Movie)
            .WithMany(movie => movie.MovieGenres)
            .HasForeignKey(movieGenre => movieGenre.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(movieGenre => movieGenre.Genre)
            .WithMany(genre => genre.MovieGenres)
            .HasForeignKey(movieGenre => movieGenre.GenreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(movieGenre => movieGenre.GenreId);
    }
}
