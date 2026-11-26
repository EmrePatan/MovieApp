using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserWatchPlatformPreferenceConfiguration : IEntityTypeConfiguration<UserWatchPlatformPreference>
{
    public void Configure(EntityTypeBuilder<UserWatchPlatformPreference> builder)
    {
        builder.ToTable("user_watch_platform_preferences");

        builder.HasKey(preference => preference.UserId);

        builder.Property(preference => preference.UserId)
            .ValueGeneratedNever();

        builder.Property(preference => preference.ProviderIds)
            .IsRequired();

        builder.Property(preference => preference.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(preference => preference.User)
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
