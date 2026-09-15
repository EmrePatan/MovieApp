using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserReleaseNotificationConfiguration : IEntityTypeConfiguration<UserReleaseNotification>
{
    public void Configure(EntityTypeBuilder<UserReleaseNotification> builder)
    {
        builder.ToTable("user_release_notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Id)
            .ValueGeneratedNever();

        builder.Property(notification => notification.UserId)
            .IsRequired();

        builder.Property(notification => notification.NotificationType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(notification => notification.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(notification => notification.AggregationWindowKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(notification => notification.Title)
            .HasMaxLength(200);

        builder.Property(notification => notification.Body)
            .HasMaxLength(500);

        builder.Property(notification => notification.CreatedAtUtc)
            .IsRequired();

        builder.Property(notification => notification.ReadAtUtc);

        builder.HasOne(notification => notification.User)
            .WithMany(user => user.ReleaseNotifications)
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(notification => notification.TvShow)
            .WithMany(tvShow => tvShow.ReleaseNotifications)
            .HasForeignKey(notification => notification.TvShowId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(notification => notification.Movie)
            .WithMany(movie => movie.ReleaseNotifications)
            .HasForeignKey(notification => notification.MovieId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(notification => notification.UserId);

        builder.HasIndex(notification => notification.TvShowId);

        builder.HasIndex(notification => notification.MovieId);

        builder.HasIndex(notification => notification.Status);

        builder.HasIndex(notification => new
            {
                notification.UserId,
                notification.CreatedAtUtc,
                notification.Id
            })
            .IsDescending(false, true, true);

        builder.HasIndex(notification => notification.UserId)
            .HasFilter("\"ReadAtUtc\" IS NULL");

        builder.HasIndex(notification => new
            {
                notification.UserId,
                notification.TvShowId,
                notification.MovieId,
                notification.NotificationType,
                notification.AggregationWindowKey
            })
            .IsUnique();

        builder.HasAlternateKey(notification => new
        {
            notification.Id,
            notification.UserId
        });

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "CK_user_release_notifications_content_ref",
            "(\"TvShowId\" IS NOT NULL AND \"MovieId\" IS NULL) OR (\"TvShowId\" IS NULL AND \"MovieId\" IS NOT NULL)"));
    }
}
