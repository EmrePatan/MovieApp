using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TvShowKeywordSourceConfiguration : IEntityTypeConfiguration<TvShowKeywordSource>
{
    public void Configure(EntityTypeBuilder<TvShowKeywordSource> builder)
    {
        builder.ToTable("tv_show_keyword_sources");

        builder.HasKey(source => new { source.TvShowId, source.KeywordId, source.Provider });

        builder.Property(source => source.Provider)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.HasOne(source => source.TvShow)
            .WithMany(tvShow => tvShow.TvShowKeywordSources)
            .HasForeignKey(source => source.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(source => source.Keyword)
            .WithMany(keyword => keyword.TvShowKeywordSources)
            .HasForeignKey(source => source.KeywordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(source => new { source.TvShowId, source.Provider });
        builder.HasIndex(source => source.KeywordId);
    }
}
