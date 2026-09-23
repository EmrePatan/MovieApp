using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Reviews;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_reviews_catalog_reference",
                "(\"MovieId\" IS NOT NULL AND \"TvShowId\" IS NULL) OR (\"MovieId\" IS NULL AND \"TvShowId\" IS NOT NULL)");
        });

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .ValueGeneratedNever();

        builder.Property(review => review.UserId)
            .IsRequired();

        builder.Property(review => review.Content)
            .IsRequired()
            .HasMaxLength(ReviewContentRules.MaxLength);

        builder.Property(review => review.AuthoringLocale)
            .HasMaxLength(10);

        builder.Property(review => review.CreatedAt)
            .IsRequired();

        builder.Property(review => review.UpdatedAt)
            .IsRequired();

        builder.HasOne(review => review.User)
            .WithMany(user => user.Reviews)
            .HasForeignKey(review => review.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(review => review.Movie)
            .WithMany(movie => movie.Reviews)
            .HasForeignKey(review => review.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(review => review.TvShow)
            .WithMany(tvShow => tvShow.Reviews)
            .HasForeignKey(review => review.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(review => review.UserId);

        builder.HasIndex(review => review.MovieId);

        builder.HasIndex(review => review.TvShowId);

        builder.HasIndex(review => review.CreatedAt);

        builder.HasIndex(review => new { review.UserId, review.MovieId })
            .IsUnique()
            .HasFilter("\"MovieId\" IS NOT NULL");

        builder.HasIndex(review => new { review.UserId, review.TvShowId })
            .IsUnique()
            .HasFilter("\"TvShowId\" IS NOT NULL");
    }
}
