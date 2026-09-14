using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class PushNotificationDeliveryConfiguration : IEntityTypeConfiguration<PushNotificationDelivery>
{
    public void Configure(EntityTypeBuilder<PushNotificationDelivery> builder)
    {
        builder.ToTable("push_notification_deliveries");

        builder.HasKey(delivery => delivery.Id);

        builder.Property(delivery => delivery.Id)
            .ValueGeneratedNever();

        builder.Property(delivery => delivery.UserReleaseNotificationId)
            .IsRequired();

        builder.Property(delivery => delivery.PushDeviceId)
            .IsRequired();

        builder.Property(delivery => delivery.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(delivery => delivery.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(delivery => delivery.ExpoTicketId)
            .HasMaxLength(128);

        builder.Property(delivery => delivery.LastErrorCode)
            .HasMaxLength(64);

        builder.Property(delivery => delivery.LastErrorMessage)
            .HasMaxLength(500);

        builder.Property(delivery => delivery.CreatedAtUtc)
            .IsRequired();

        builder.Property(delivery => delivery.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(delivery => delivery.UserReleaseNotification)
            .WithMany()
            .HasForeignKey(delivery => delivery.UserReleaseNotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(delivery => delivery.PushDevice)
            .WithMany()
            .HasForeignKey(delivery => delivery.PushDeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(delivery => new { delivery.UserReleaseNotificationId, delivery.PushDeviceId })
            .IsUnique();

        builder.HasIndex(delivery => delivery.Status);

        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAtUtc });

        builder.HasIndex(delivery => delivery.ExpoTicketId);

        builder.HasIndex(delivery => delivery.ClaimedUntilUtc);
    }
}
