using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TmdbChangesFailedTargetConfiguration
    : IEntityTypeConfiguration<TmdbChangesFailedTarget>
{
    public void Configure(EntityTypeBuilder<TmdbChangesFailedTarget> builder)
    {
        builder.ToTable("tmdb_changes_failed_targets");

        builder.HasKey(target => new { target.CheckpointKey, target.TargetId });

        builder.Property(target => target.CheckpointKey)
            .HasMaxLength(64);
    }
}
