using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class ContentSearchTitleConfiguration : IEntityTypeConfiguration<ContentSearchTitle>
{
    public void Configure(EntityTypeBuilder<ContentSearchTitle> builder)
    {
        builder.ToTable("content_search_titles");

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .ValueGeneratedNever();

        builder.Property(row => row.ContentType)
            .IsRequired();

        builder.Property(row => row.ContentId)
            .IsRequired();

        builder.Property(row => row.Title)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.TitleMaxLength);

        builder.Property(row => row.NormalizedTitle)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.TitleMaxLength);

        builder.Property(row => row.TitleKind)
            .IsRequired();

        builder.Property(row => row.Source)
            .IsRequired();

        builder.Property(row => row.LanguageCode)
            .HasMaxLength(ConfigurationConstants.LanguageMaxLength);

        builder.Property(row => row.CountryCode)
            .HasMaxLength(8);

        builder.Property(row => row.ProviderTitleType)
            .HasMaxLength(64);

        builder.HasIndex(row => new { row.ContentType, row.ContentId })
            .HasDatabaseName("IX_content_search_titles_ContentType_ContentId");

        builder.HasIndex(row => new { row.ContentType, row.ContentId, row.NormalizedTitle })
            .IsUnique()
            .HasDatabaseName("UX_content_search_titles_ContentType_ContentId_NormalizedTitle");
    }
}
