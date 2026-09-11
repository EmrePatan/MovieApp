using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class WatchedEpisodeConfiguration : IEntityTypeConfiguration<WatchedEpisode>
{
    public void Configure(EntityTypeBuilder<WatchedEpisode> builder)
    {
        builder.ToTable("watched_episodes");

        builder.HasKey(watchedEpisode => watchedEpisode.Id);

        builder.Property(watchedEpisode => watchedEpisode.Id)
            .ValueGeneratedNever();

        builder.Property(watchedEpisode => watchedEpisode.UserId)
            .IsRequired();

        builder.Property(watchedEpisode => watchedEpisode.EpisodeId)
            .IsRequired();

        builder.Property(watchedEpisode => watchedEpisode.WatchedAt)
            .IsRequired();

        builder.Property(watchedEpisode => watchedEpisode.CreatedAt)
            .IsRequired();

        builder.Property(watchedEpisode => watchedEpisode.UpdatedAt)
            .IsRequired();

        builder.HasOne(watchedEpisode => watchedEpisode.User)
            .WithMany(user => user.WatchedEpisodes)
            .HasForeignKey(watchedEpisode => watchedEpisode.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(watchedEpisode => watchedEpisode.Episode)
            .WithMany(episode => episode.WatchedEpisodes)
            .HasForeignKey(watchedEpisode => watchedEpisode.EpisodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(watchedEpisode => watchedEpisode.UserId);

        builder.HasIndex(watchedEpisode => watchedEpisode.EpisodeId);

        builder.HasIndex(watchedEpisode => watchedEpisode.WatchedAt);

        builder.HasIndex(watchedEpisode => new { watchedEpisode.UserId, watchedEpisode.EpisodeId })
            .IsUnique();

        builder.HasIndex(watchedEpisode => new { watchedEpisode.UserId, watchedEpisode.WatchedAt });
    }
}
