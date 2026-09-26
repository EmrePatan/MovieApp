using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class ContentLocalizedPosterConfiguration : IEntityTypeConfiguration<ContentLocalizedPoster>
{
    public void Configure(EntityTypeBuilder<ContentLocalizedPoster> builder)
    {
        builder.ToTable("content_localized_posters");

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .ValueGeneratedNever();

        builder.Property(row => row.ContentType)
            .IsRequired();

        builder.Property(row => row.ContentId)
            .IsRequired();

        builder.Property(row => row.LanguageKey)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.LanguageMaxLength);

        builder.Property(row => row.PosterPath)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(row => row.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(row => new { row.ContentType, row.ContentId, row.LanguageKey })
            .IsUnique()
            .HasDatabaseName("UX_content_localized_posters_ContentType_ContentId_LanguageKey");

        builder.HasIndex(row => new { row.LanguageKey, row.ContentType, row.ContentId })
            .HasDatabaseName("IX_content_localized_posters_LanguageKey_ContentType_ContentId");
    }
}
