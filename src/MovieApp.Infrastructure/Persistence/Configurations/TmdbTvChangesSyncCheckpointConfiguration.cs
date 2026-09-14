using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TmdbTvChangesSyncCheckpointConfiguration
    : IEntityTypeConfiguration<TmdbTvChangesSyncCheckpoint>
{
    public void Configure(EntityTypeBuilder<TmdbTvChangesSyncCheckpoint> builder)
    {
        builder.ToTable("tmdb_tv_changes_sync_checkpoints");

        builder.HasKey(checkpoint => checkpoint.CheckpointKey);

        builder.Property(checkpoint => checkpoint.CheckpointKey)
            .HasMaxLength(64);

        builder.Property(checkpoint => checkpoint.UpdatedAtUtc)
            .IsRequired();
    }
}
