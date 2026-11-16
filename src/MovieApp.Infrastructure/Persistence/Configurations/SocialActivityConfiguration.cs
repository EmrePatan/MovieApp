using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SocialActivityConfiguration : IEntityTypeConfiguration<SocialActivity>
{
    public void Configure(EntityTypeBuilder<SocialActivity> builder)
    {
        builder.ToTable("social_activities");
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Id).ValueGeneratedNever();
        builder.Property(activity => activity.ActorId).IsRequired();
        builder.Property(activity => activity.ActivityType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(activity => activity.SourceEntityId).IsRequired();
        builder.Property(activity => activity.TargetContentId).IsRequired();
        builder.Property(activity => activity.TargetContentType).HasMaxLength(16).IsRequired();
        builder.Property(activity => activity.CreatedAt).IsRequired();
        builder.Property(activity => activity.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasOne<User>().WithMany().HasForeignKey(activity => activity.ActorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(activity => new { activity.ActorId, activity.ActivityType, activity.SourceEntityId })
            .IsUnique();
        builder.HasIndex(activity => new { activity.CreatedAt, activity.Id });
        builder.HasIndex(activity => new { activity.ActorId, activity.CreatedAt });
        builder.HasIndex(activity => new { activity.TargetContentType, activity.TargetContentId });
    }
}
