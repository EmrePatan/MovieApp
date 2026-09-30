using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class MovieKeywordSourceConfiguration : IEntityTypeConfiguration<MovieKeywordSource>
{
    public void Configure(EntityTypeBuilder<MovieKeywordSource> builder)
    {
        builder.ToTable("movie_keyword_sources");

        builder.HasKey(source => new { source.MovieId, source.KeywordId, source.Provider });

        builder.Property(source => source.Provider)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.HasOne(source => source.Movie)
            .WithMany(movie => movie.MovieKeywordSources)
            .HasForeignKey(source => source.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(source => source.Keyword)
            .WithMany(keyword => keyword.MovieKeywordSources)
            .HasForeignKey(source => source.KeywordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(source => new { source.MovieId, source.Provider });
        builder.HasIndex(source => source.KeywordId);
    }
}
