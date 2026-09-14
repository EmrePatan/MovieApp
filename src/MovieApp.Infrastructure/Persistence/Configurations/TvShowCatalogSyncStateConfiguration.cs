using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TvShowCatalogSyncStateConfiguration : IEntityTypeConfiguration<TvShowCatalogSyncState>
{
    public void Configure(EntityTypeBuilder<TvShowCatalogSyncState> builder)
    {
        builder.ToTable("tv_show_catalog_sync_states");

        builder.HasKey(state => state.TvShowId);

        builder.Property(state => state.TvShowId)
            .ValueGeneratedNever();

        builder.Property(state => state.LastRefreshReason)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(state => state.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(state => state.TvShow)
            .WithOne(tvShow => tvShow.CatalogSyncState)
            .HasForeignKey<TvShowCatalogSyncState>(state => state.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
