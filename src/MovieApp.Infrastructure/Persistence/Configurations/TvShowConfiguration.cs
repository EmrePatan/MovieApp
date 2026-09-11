using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TvShowConfiguration : IEntityTypeConfiguration<TvShow>
{
    public void Configure(EntityTypeBuilder<TvShow> builder)
    {
        builder.ToTable("tv_shows");

        builder.HasKey(tvShow => tvShow.Id);

        builder.Property(tvShow => tvShow.Id)
            .ValueGeneratedNever();

        builder.Property(tvShow => tvShow.Title)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.TitleMaxLength);

        builder.Property(tvShow => tvShow.OriginalTitle)
            .HasMaxLength(ConfigurationConstants.TitleMaxLength);

        builder.Property(tvShow => tvShow.Overview)
            .HasMaxLength(ConfigurationConstants.OverviewMaxLength);

        builder.Property(tvShow => tvShow.PosterPath)
            .HasMaxLength(ConfigurationConstants.PathMaxLength);

        builder.Property(tvShow => tvShow.BackdropPath)
            .HasMaxLength(ConfigurationConstants.PathMaxLength);

        builder.Property(tvShow => tvShow.OriginalLanguage)
            .HasMaxLength(ConfigurationConstants.LanguageMaxLength);

        builder.Property(tvShow => tvShow.ImdbId)
            .HasMaxLength(ConfigurationConstants.ImdbIdMaxLength);

        builder.Property(tvShow => tvShow.VoteAverage)
            .HasPrecision(ConfigurationConstants.VoteAveragePrecision, ConfigurationConstants.VoteAverageScale);

        builder.Property(tvShow => tvShow.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(tvShow => tvShow.CreatedAt)
            .IsRequired();

        builder.Property(tvShow => tvShow.UpdatedAt)
            .IsRequired();

        builder.HasIndex(tvShow => tvShow.TmdbId).AsUniqueExternalIdIndex("TmdbId");
        builder.HasIndex(tvShow => tvShow.TvdbId).AsUniqueExternalIdIndex("TvdbId");
        builder.HasIndex(tvShow => tvShow.ImdbId).AsUniqueExternalIdIndex("ImdbId");

        builder.HasIndex(tvShow => tvShow.Title);
        builder.HasIndex(tvShow => tvShow.FirstAirDate);
        builder.HasIndex(tvShow => tvShow.Status);
    }
}
