using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SocialCommentConfiguration : IEntityTypeConfiguration<SocialComment>
{
    public void Configure(EntityTypeBuilder<SocialComment> builder)
    {
        builder.ToTable("social_comments", table =>
            table.HasCheckConstraint("CK_social_comments_not_self_parent", "\"ParentCommentId\" IS NULL OR \"ParentCommentId\" <> \"Id\""));
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).ValueGeneratedNever();
        builder.Property(comment => comment.Content).HasMaxLength(4000).IsRequired();
        builder.Property(comment => comment.AuthoringLocale).HasMaxLength(10);
        builder.Property(comment => comment.CreatedAt).IsRequired();
        builder.Property(comment => comment.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.HasOne<SocialActivity>().WithMany().HasForeignKey(comment => comment.ActivityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(comment => comment.AuthorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SocialComment>().WithMany().HasForeignKey(comment => comment.ParentCommentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(comment => new { comment.ActivityId, comment.CreatedAt, comment.Id });
        builder.HasIndex(comment => comment.ParentCommentId);
        builder.HasIndex(comment => new { comment.AuthorId, comment.CreatedAt });
    }
}