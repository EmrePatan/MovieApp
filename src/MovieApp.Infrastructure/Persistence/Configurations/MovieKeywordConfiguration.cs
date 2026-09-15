using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class MovieKeywordConfiguration : IEntityTypeConfiguration<MovieKeyword>
{
    public void Configure(EntityTypeBuilder<MovieKeyword> builder)
    {
        builder.ToTable("movie_keywords");

        builder.HasKey(movieKeyword => new { movieKeyword.MovieId, movieKeyword.KeywordId });

        builder.HasOne(movieKeyword => movieKeyword.Movie)
            .WithMany(movie => movie.MovieKeywords)
            .HasForeignKey(movieKeyword => movieKeyword.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(movieKeyword => movieKeyword.Keyword)
            .WithMany(keyword => keyword.MovieKeywords)
            .HasForeignKey(movieKeyword => movieKeyword.KeywordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(movieKeyword => movieKeyword.KeywordId);
    }
}
