using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class KeywordDisplayProfileConfiguration : IEntityTypeConfiguration<KeywordDisplayProfile>
{
    public void Configure(EntityTypeBuilder<KeywordDisplayProfile> builder)
    {
        builder.ToTable("keyword_display_profiles");

        builder.HasKey(profile => profile.KeywordId);

        builder.Property(profile => profile.DocumentFrequency).IsRequired();
        builder.Property(profile => profile.MovieTitleCount).IsRequired();
        builder.Property(profile => profile.TvTitleCount).IsRequired();
        builder.Property(profile => profile.DisplayRank).IsRequired();
        builder.Property(profile => profile.Displayable).IsRequired();
        builder.Property(profile => profile.UpdatedAtUtc).IsRequired();

        builder.HasIndex(profile => new { profile.Displayable, profile.DisplayRank });

        builder.HasOne(profile => profile.Keyword)
            .WithOne(keyword => keyword.DisplayProfile)
            .HasForeignKey<KeywordDisplayProfile>(profile => profile.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
