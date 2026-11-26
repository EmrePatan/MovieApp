using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class StreamingLeavingItemConfiguration : IEntityTypeConfiguration<StreamingLeavingItem>
{
    public void Configure(EntityTypeBuilder<StreamingLeavingItem> builder)
    {
        builder.ToTable("streaming_leaving_items");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Region).HasMaxLength(2).IsRequired();
        builder.Property(item => item.ServiceId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ServiceName).HasMaxLength(128).IsRequired();
        builder.Property(item => item.WatchLink).HasMaxLength(2048);

        builder.HasIndex(item => new { item.Region, item.ServiceId, item.MediaType, item.ContentId })
            .IsUnique();
        builder.HasIndex(item => new { item.Region, item.LeavesAtUtc });
    }
}

internal sealed class StreamingLeavingRegionConfiguration : IEntityTypeConfiguration<StreamingLeavingRegion>
{
    public void Configure(EntityTypeBuilder<StreamingLeavingRegion> builder)
    {
        builder.ToTable("streaming_leaving_regions");

        builder.HasKey(state => state.Region);
        builder.Property(state => state.Region).HasMaxLength(2).ValueGeneratedNever();
    }
}

internal sealed class StreamingApiUsageConfiguration : IEntityTypeConfiguration<StreamingApiUsage>
{
    public void Configure(EntityTypeBuilder<StreamingApiUsage> builder)
    {
        builder.ToTable("streaming_api_usage");

        builder.HasKey(usage => new { usage.PeriodStart, usage.Region });
        builder.Property(usage => usage.Region).HasMaxLength(2);
    }
}
