using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserReleaseNotificationEventConfiguration
    : IEntityTypeConfiguration<UserReleaseNotificationEvent>
{
    public void Configure(EntityTypeBuilder<UserReleaseNotificationEvent> builder)
    {
        builder.ToTable("user_release_notification_events");

        builder.HasKey(notificationEvent => new
        {
            notificationEvent.UserReleaseNotificationId,
            notificationEvent.CatalogReleaseEventId
        });

        builder.Property(notificationEvent => notificationEvent.UserId)
            .IsRequired();

        builder.HasOne(notificationEvent => notificationEvent.Notification)
            .WithMany(notification => notification.NotificationEvents)
            .HasForeignKey(notificationEvent => new
            {
                notificationEvent.UserReleaseNotificationId,
                notificationEvent.UserId
            })
            .HasPrincipalKey(notification => new
            {
                notification.Id,
                notification.UserId
            })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(notificationEvent => notificationEvent.ReleaseEvent)
            .WithMany(releaseEvent => releaseEvent.NotificationEvents)
            .HasForeignKey(notificationEvent => notificationEvent.CatalogReleaseEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(notificationEvent => notificationEvent.User)
            .WithMany()
            .HasForeignKey(notificationEvent => notificationEvent.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(notificationEvent => notificationEvent.CatalogReleaseEventId);

        builder.HasIndex(notificationEvent => new
            {
                notificationEvent.UserReleaseNotificationId,
                notificationEvent.CatalogReleaseEventId
            })
            .IsUnique();

        builder.HasIndex(notificationEvent => new
            {
                notificationEvent.UserId,
                notificationEvent.CatalogReleaseEventId
            })
            .IsUnique();
    }
}
