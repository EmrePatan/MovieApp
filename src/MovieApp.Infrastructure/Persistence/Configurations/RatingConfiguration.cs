using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Ratings;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("ratings", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ratings_score_range",
                $"\"Score\" >= {RatingScoreRules.MinScore} AND \"Score\" <= {RatingScoreRules.MaxScore}");

            tableBuilder.HasCheckConstraint(
                "CK_ratings_catalog_reference",
                "(\"MovieId\" IS NOT NULL AND \"TvShowId\" IS NULL) OR (\"MovieId\" IS NULL AND \"TvShowId\" IS NOT NULL)");
        });

        builder.HasKey(rating => rating.Id);

        builder.Property(rating => rating.Id)
            .ValueGeneratedNever();

        builder.Property(rating => rating.UserId)
            .IsRequired();

        builder.Property(rating => rating.Score)
            .IsRequired();

        builder.Property(rating => rating.CreatedAt)
            .IsRequired();

        builder.Property(rating => rating.UpdatedAt)
            .IsRequired();

        builder.HasOne(rating => rating.User)
            .WithMany(user => user.Ratings)
            .HasForeignKey(rating => rating.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rating => rating.Movie)
            .WithMany(movie => movie.Ratings)
            .HasForeignKey(rating => rating.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rating => rating.TvShow)
            .WithMany(tvShow => tvShow.Ratings)
            .HasForeignKey(rating => rating.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rating => rating.UserId);

        builder.HasIndex(rating => rating.MovieId);

        builder.HasIndex(rating => rating.TvShowId);

        builder.HasIndex(rating => rating.CreatedAt);

        builder.HasIndex(rating => new { rating.UserId, rating.MovieId })
            .IsUnique()
            .HasFilter("\"MovieId\" IS NOT NULL");

        builder.HasIndex(rating => new { rating.UserId, rating.TvShowId })
            .IsUnique()
            .HasFilter("\"TvShowId\" IS NOT NULL");
    }
}
