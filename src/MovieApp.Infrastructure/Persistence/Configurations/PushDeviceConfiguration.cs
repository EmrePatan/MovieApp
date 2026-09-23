using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        builder.ToTable("push_devices");

        builder.HasKey(device => device.Id);

        builder.Property(device => device.Id)
            .ValueGeneratedNever();

        builder.Property(device => device.UserId)
            .IsRequired();

        builder.Property(device => device.ExpoPushToken)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(device => device.Platform)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(device => device.DeviceIdentifier)
            .HasMaxLength(128);

        builder.Property(device => device.ContentLocale)
            .HasMaxLength(10);

        builder.Property(device => device.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(device => device.CreatedAtUtc)
            .IsRequired();

        builder.Property(device => device.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(device => device.User)
            .WithMany(user => user.PushDevices)
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(device => device.UserId);

        builder.HasIndex(device => device.ExpoPushToken)
            .IsUnique();

        builder.HasIndex(device => new { device.UserId, device.IsActive });
    }
}
