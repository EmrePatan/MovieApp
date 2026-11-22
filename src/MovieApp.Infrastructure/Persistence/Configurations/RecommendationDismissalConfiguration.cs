using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class RecommendationDismissalConfiguration : IEntityTypeConfiguration<RecommendationDismissal>
{
    public void Configure(EntityTypeBuilder<RecommendationDismissal> builder)
    {
        builder.ToTable("recommendation_dismissals");

        builder.HasKey(dismissal => dismissal.Id);

        builder.Property(dismissal => dismissal.Id)
            .ValueGeneratedNever();

        builder.Property(dismissal => dismissal.UserId)
            .IsRequired();

        builder.Property(dismissal => dismissal.CreatedAt)
            .IsRequired();

        builder.HasOne(dismissal => dismissal.User)
            .WithMany()
            .HasForeignKey(dismissal => dismissal.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dismissal => dismissal.Movie)
            .WithMany()
            .HasForeignKey(dismissal => dismissal.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dismissal => dismissal.TvShow)
            .WithMany()
            .HasForeignKey(dismissal => dismissal.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(dismissal => dismissal.UserId);

        builder.HasIndex(dismissal => new { dismissal.UserId, dismissal.MovieId })
            .IsUnique()
            .HasFilter("\"MovieId\" IS NOT NULL");

        builder.HasIndex(dismissal => new { dismissal.UserId, dismissal.TvShowId })
            .IsUnique()
            .HasFilter("\"TvShowId\" IS NOT NULL");
    }
}
