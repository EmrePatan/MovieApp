using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserPrivacySettingsConfiguration : IEntityTypeConfiguration<UserPrivacySettings>
{
    public void Configure(EntityTypeBuilder<UserPrivacySettings> builder)
    {
        builder.ToTable("user_privacy_settings");

        builder.HasKey(settings => settings.UserId);
        builder.Property(settings => settings.UserId).ValueGeneratedNever();
        builder.Property(settings => settings.IsPrivate).IsRequired().HasDefaultValue(false);
        builder.Property(settings => settings.ShowWatched).IsRequired().HasDefaultValue(true);
        builder.Property(settings => settings.ShowRatings).IsRequired().HasDefaultValue(true);
        builder.Property(settings => settings.ShowFavorites).IsRequired().HasDefaultValue(true);
        builder.Property(settings => settings.ShowReviews).IsRequired().HasDefaultValue(true);
        builder.Property(settings => settings.UpdatedAt).IsRequired();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserPrivacySettings>(settings => settings.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
