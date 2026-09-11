using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.ToTable("movies");

        builder.HasKey(movie => movie.Id);

        builder.Property(movie => movie.Id)
            .ValueGeneratedNever();

        builder.Property(movie => movie.Title)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.TitleMaxLength);

        builder.Property(movie => movie.OriginalTitle)
            .HasMaxLength(ConfigurationConstants.TitleMaxLength);

        builder.Property(movie => movie.Overview)
            .HasMaxLength(ConfigurationConstants.OverviewMaxLength);

        builder.Property(movie => movie.PosterPath)
            .HasMaxLength(ConfigurationConstants.PathMaxLength);

        builder.Property(movie => movie.BackdropPath)
            .HasMaxLength(ConfigurationConstants.PathMaxLength);

        builder.Property(movie => movie.OriginalLanguage)
            .HasMaxLength(ConfigurationConstants.LanguageMaxLength);

        builder.Property(movie => movie.ImdbId)
            .HasMaxLength(ConfigurationConstants.ImdbIdMaxLength);

        builder.Property(movie => movie.VoteAverage)
            .HasPrecision(ConfigurationConstants.VoteAveragePrecision, ConfigurationConstants.VoteAverageScale);

        builder.Property(movie => movie.CreatedAt)
            .IsRequired();

        builder.Property(movie => movie.UpdatedAt)
            .IsRequired();

        builder.HasIndex(movie => movie.TmdbId).AsUniqueExternalIdIndex("TmdbId");
        builder.HasIndex(movie => movie.TvdbId).AsUniqueExternalIdIndex("TvdbId");
        builder.HasIndex(movie => movie.ImdbId).AsUniqueExternalIdIndex("ImdbId");

        builder.HasIndex(movie => movie.Title);
        builder.HasIndex(movie => movie.ReleaseDate);
    }
}
