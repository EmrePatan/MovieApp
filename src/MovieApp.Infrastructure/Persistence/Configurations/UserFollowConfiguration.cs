using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        builder.ToTable("user_follows", table =>
            table.HasCheckConstraint("CK_user_follows_distinct_users", "\"FollowerId\" <> \"FollowedUserId\""));

        builder.HasKey(follow => follow.Id);

        builder.Property(follow => follow.Id).ValueGeneratedNever();
        builder.Property(follow => follow.FollowerId).IsRequired();
        builder.Property(follow => follow.FollowedUserId).IsRequired();
        builder.Property(follow => follow.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(follow => follow.CreatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(follow => follow.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(follow => follow.FollowedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(follow => new { follow.FollowerId, follow.FollowedUserId }).IsUnique();
        builder.HasIndex(follow => new { follow.FollowedUserId, follow.Status, follow.CreatedAt });
        builder.HasIndex(follow => new { follow.FollowerId, follow.Status, follow.CreatedAt });
    }
}
