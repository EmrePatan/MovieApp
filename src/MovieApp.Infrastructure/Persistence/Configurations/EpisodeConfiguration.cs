using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class EpisodeConfiguration : IEntityTypeConfiguration<Episode>
{
    public void Configure(EntityTypeBuilder<Episode> builder)
    {
        builder.ToTable("episodes");

        builder.HasKey(episode => episode.Id);

        builder.Property(episode => episode.Id)
            .ValueGeneratedNever();

        builder.Property(episode => episode.Name)
            .HasMaxLength(ConfigurationConstants.TitleMaxLength);

        builder.Property(episode => episode.Overview)
            .HasMaxLength(ConfigurationConstants.OverviewMaxLength);

        builder.Property(episode => episode.StillPath)
            .HasMaxLength(ConfigurationConstants.PathMaxLength);

        builder.Property(episode => episode.ImdbId)
            .HasMaxLength(ConfigurationConstants.ImdbIdMaxLength);

        builder.Property(episode => episode.VoteAverage)
            .HasPrecision(ConfigurationConstants.VoteAveragePrecision, ConfigurationConstants.VoteAverageScale);

        builder.Property(episode => episode.CreatedAt)
            .IsRequired();

        builder.Property(episode => episode.UpdatedAt)
            .IsRequired();

        builder.HasOne(episode => episode.Season)
            .WithMany(season => season.Episodes)
            .HasForeignKey(episode => episode.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(episode => new { episode.SeasonId, episode.EpisodeNumber })
            .IsUnique();

        builder.HasIndex(episode => episode.AirDate);

        builder.HasIndex(episode => episode.TmdbId).AsUniqueExternalIdIndex("TmdbId");
        builder.HasIndex(episode => episode.TvdbId).AsUniqueExternalIdIndex("TvdbId");
        builder.HasIndex(episode => episode.ImdbId).AsUniqueExternalIdIndex("ImdbId");
    }
}
