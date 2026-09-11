using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("seasons");

        builder.HasKey(season => season.Id);

        builder.Property(season => season.Id)
            .ValueGeneratedNever();

        builder.Property(season => season.Name)
            .HasMaxLength(ConfigurationConstants.TitleMaxLength);

        builder.Property(season => season.Overview)
            .HasMaxLength(ConfigurationConstants.OverviewMaxLength);

        builder.Property(season => season.PosterPath)
            .HasMaxLength(ConfigurationConstants.PathMaxLength);

        builder.Property(season => season.CreatedAt)
            .IsRequired();

        builder.Property(season => season.UpdatedAt)
            .IsRequired();

        builder.HasOne(season => season.TvShow)
            .WithMany(tvShow => tvShow.Seasons)
            .HasForeignKey(season => season.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(season => new { season.TvShowId, season.SeasonNumber })
            .IsUnique();

        builder.HasIndex(season => season.TmdbId).AsUniqueExternalIdIndex("TmdbId");
        builder.HasIndex(season => season.TvdbId).AsUniqueExternalIdIndex("TvdbId");
    }
}
