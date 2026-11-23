using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("user_notification_preferences");

        builder.HasKey(preference => preference.UserId);

        builder.Property(preference => preference.UserId)
            .ValueGeneratedNever();

        builder.Property(preference => preference.MessagePushEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(preference => preference.ReleasePushEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(preference => preference.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(preference => preference.User)
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
