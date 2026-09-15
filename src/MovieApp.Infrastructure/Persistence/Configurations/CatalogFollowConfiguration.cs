using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class CatalogFollowConfiguration : IEntityTypeConfiguration<CatalogFollow>
{
    public void Configure(EntityTypeBuilder<CatalogFollow> builder)
    {
        builder.ToTable("catalog_follows");

        builder.HasKey(follow => follow.Id);

        builder.Property(follow => follow.Id)
            .ValueGeneratedNever();

        builder.Property(follow => follow.UserId)
            .IsRequired();

        builder.Property(follow => follow.ContentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(follow => follow.ContentId)
            .IsRequired();

        builder.Property(follow => follow.NotifyMovieRelease)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(follow => follow.NotifyNewSeasons)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(follow => follow.NotifyNewEpisodes)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(follow => follow.CreatedAt)
            .IsRequired();

        builder.Property(follow => follow.UpdatedAt)
            .IsRequired();

        builder.HasOne(follow => follow.User)
            .WithMany(user => user.CatalogFollows)
            .HasForeignKey(follow => follow.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(follow => follow.UserId);

        builder.HasIndex(follow => new { follow.UserId, follow.CreatedAt });

        builder.HasIndex(follow => new { follow.ContentType, follow.ContentId });

        builder.HasIndex(follow => new { follow.UserId, follow.ContentType, follow.ContentId })
            .IsUnique();

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "CK_catalog_follows_content_type",
            "\"ContentType\" IN ('Movie', 'Tv')"));
    }
}
