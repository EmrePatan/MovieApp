using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SocialCommentLikeConfiguration : IEntityTypeConfiguration<SocialCommentLike>
{
    public void Configure(EntityTypeBuilder<SocialCommentLike> builder)
    {
        builder.ToTable("social_comment_likes");
        builder.HasKey(like => like.Id);
        builder.Property(like => like.Id).ValueGeneratedNever();
        builder.Property(like => like.CommentId).IsRequired();
        builder.Property(like => like.UserId).IsRequired();
        builder.Property(like => like.CreatedAt).IsRequired();
        builder.HasOne<SocialComment>().WithMany().HasForeignKey(like => like.CommentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(like => like.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(like => new { like.CommentId, like.UserId }).IsUnique();
        builder.HasIndex(like => like.UserId);
    }
}
