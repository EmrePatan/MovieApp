using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SocialActivityLikeConfiguration : IEntityTypeConfiguration<SocialActivityLike>
{
    public void Configure(EntityTypeBuilder<SocialActivityLike> builder)
    {
        builder.ToTable("social_activity_likes");
        builder.HasKey(like => like.Id);
        builder.Property(like => like.Id).ValueGeneratedNever();
        builder.Property(like => like.ActivityId).IsRequired();
        builder.Property(like => like.UserId).IsRequired();
        builder.Property(like => like.CreatedAt).IsRequired();
        builder.HasOne<SocialActivity>().WithMany().HasForeignKey(like => like.ActivityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(like => like.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(like => new { like.ActivityId, like.UserId }).IsUnique();
        builder.HasIndex(like => like.UserId);
    }
}
