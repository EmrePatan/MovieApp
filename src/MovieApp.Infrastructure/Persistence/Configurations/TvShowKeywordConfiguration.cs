using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TvShowKeywordConfiguration : IEntityTypeConfiguration<TvShowKeyword>
{
    public void Configure(EntityTypeBuilder<TvShowKeyword> builder)
    {
        builder.ToTable("tv_show_keywords");

        builder.HasKey(tvShowKeyword => new { tvShowKeyword.TvShowId, tvShowKeyword.KeywordId });

        builder.HasOne(tvShowKeyword => tvShowKeyword.TvShow)
            .WithMany(tvShow => tvShow.TvShowKeywords)
            .HasForeignKey(tvShowKeyword => tvShowKeyword.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tvShowKeyword => tvShowKeyword.Keyword)
            .WithMany(keyword => keyword.TvShowKeywords)
            .HasForeignKey(tvShowKeyword => tvShowKeyword.KeywordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tvShowKeyword => tvShowKeyword.KeywordId);
    }
}
