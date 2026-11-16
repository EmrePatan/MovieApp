using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SocialNotificationConfiguration : IEntityTypeConfiguration<SocialNotification>
{
    public void Configure(EntityTypeBuilder<SocialNotification> builder)
    {
        builder.ToTable("social_notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.NotificationType).HasMaxLength(32).IsRequired();
        builder.Property(notification => notification.CreatedAt).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(notification => notification.RecipientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(notification => notification.ActorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SocialActivity>().WithMany().HasForeignKey(notification => notification.ActivityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SocialComment>().WithMany().HasForeignKey(notification => notification.CommentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserFollow>().WithMany().HasForeignKey(notification => notification.FollowId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(notification => new { notification.RecipientId, notification.CreatedAt, notification.Id });
        builder.HasIndex(notification => notification.RecipientId).HasFilter("\"ReadAt\" IS NULL");
    }
}
