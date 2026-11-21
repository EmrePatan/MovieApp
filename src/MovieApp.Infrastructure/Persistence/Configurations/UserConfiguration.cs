using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .ValueGeneratedNever();

        builder.Property(user => user.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(user => user.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(user => user.PendingEmail)
            .HasMaxLength(320);

        builder.Property(user => user.UserName)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(512);

        builder.Property(user => user.DisplayName)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(user => user.Bio)
            .HasMaxLength(300);

        builder.Property(user => user.SocialX)
            .HasMaxLength(64);

        builder.Property(user => user.SocialInstagram)
            .HasMaxLength(64);

        builder.Property(user => user.SocialSnapchat)
            .HasMaxLength(64);

        builder.Property(user => user.SocialTiktok)
            .HasMaxLength(64);

        builder.Property(user => user.SocialYoutube)
            .HasMaxLength(64);

        builder.Property(user => user.SocialTwitch)
            .HasMaxLength(64);

        builder.Property(user => user.SocialThreads)
            .HasMaxLength(64);

        builder.Property(user => user.SocialWebsite)
            .HasMaxLength(200);

        builder.Property(user => user.CustomAvatarStorageKey)
            .HasMaxLength(512);

        builder.Property(user => user.ProfileCoverPosterPath)
            .HasMaxLength(512);

        builder.Property(user => user.CustomProfileCoverStorageKey)
            .HasMaxLength(512);

        builder.Property(user => user.IsActive)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .IsRequired();

        builder.Property(user => user.SecurityStamp)
            .IsRequired();

        builder.Property(user => user.EmailVerifiedAtUtc);

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique();
    }
}
