using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class MovieRegionalReleaseConfiguration : IEntityTypeConfiguration<MovieRegionalRelease>
{
    public void Configure(EntityTypeBuilder<MovieRegionalRelease> builder)
    {
        builder.ToTable("movie_regional_releases");

        builder.HasKey(regionalRelease => new { regionalRelease.MovieId, regionalRelease.Region });

        builder.Property(regionalRelease => regionalRelease.Region)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(regionalRelease => regionalRelease.EffectiveReleaseType)
            .HasConversion<int?>();

        builder.Property(regionalRelease => regionalRelease.Certification)
            .HasMaxLength(32);

        builder.Property(regionalRelease => regionalRelease.IsFallbackGlobal)
            .IsRequired();

        builder.Property(regionalRelease => regionalRelease.SyncedAtUtc)
            .IsRequired();

        builder.HasOne(regionalRelease => regionalRelease.Movie)
            .WithMany(movie => movie.RegionalReleases)
            .HasForeignKey(regionalRelease => regionalRelease.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(regionalRelease => new { regionalRelease.Region, regionalRelease.EffectiveReleaseDate });
    }
}
