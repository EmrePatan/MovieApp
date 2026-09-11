using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class WatchedMovieConfiguration : IEntityTypeConfiguration<WatchedMovie>
{
    public void Configure(EntityTypeBuilder<WatchedMovie> builder)
    {
        builder.ToTable("watched_movies");

        builder.HasKey(watchedMovie => watchedMovie.Id);

        builder.Property(watchedMovie => watchedMovie.Id)
            .ValueGeneratedNever();

        builder.Property(watchedMovie => watchedMovie.UserId)
            .IsRequired();

        builder.Property(watchedMovie => watchedMovie.MovieId)
            .IsRequired();

        builder.Property(watchedMovie => watchedMovie.WatchedAt)
            .IsRequired();

        builder.Property(watchedMovie => watchedMovie.CreatedAt)
            .IsRequired();

        builder.Property(watchedMovie => watchedMovie.UpdatedAt)
            .IsRequired();

        builder.HasOne(watchedMovie => watchedMovie.User)
            .WithMany(user => user.WatchedMovies)
            .HasForeignKey(watchedMovie => watchedMovie.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(watchedMovie => watchedMovie.Movie)
            .WithMany(movie => movie.WatchedMovies)
            .HasForeignKey(watchedMovie => watchedMovie.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(watchedMovie => watchedMovie.UserId);

        builder.HasIndex(watchedMovie => watchedMovie.MovieId);

        builder.HasIndex(watchedMovie => watchedMovie.WatchedAt);

        builder.HasIndex(watchedMovie => new { watchedMovie.UserId, watchedMovie.MovieId })
            .IsUnique();

        builder.HasIndex(watchedMovie => new { watchedMovie.UserId, watchedMovie.WatchedAt });
    }
}
