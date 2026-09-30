using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class KeywordLocalizationConfiguration : IEntityTypeConfiguration<KeywordLocalization>
{
    public void Configure(EntityTypeBuilder<KeywordLocalization> builder)
    {
        builder.ToTable("keyword_localizations");

        builder.HasKey(localization => new { localization.KeywordId, localization.Locale });

        builder.Property(localization => localization.Locale)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.LanguageMaxLength);

        builder.Property(localization => localization.Name)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(localization => localization.NormalizedName)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(localization => localization.TranslationSource)
            .IsRequired()
            .HasDefaultValue(KeywordTranslationSource.Machine);

        builder.Property(localization => localization.ReviewStatus)
            .IsRequired()
            .HasDefaultValue(KeywordTranslationReviewStatus.Unreviewed);

        builder.Property(localization => localization.SourceTextHash)
            .IsRequired()
            .HasMaxLength(64)
            .HasDefaultValue(string.Empty);

        builder.Property(localization => localization.CreatedAtUtc)
            .IsRequired();

        builder.Property(localization => localization.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(localization => localization.Keyword)
            .WithMany(keyword => keyword.Localizations)
            .HasForeignKey(localization => localization.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);

        // B-tree supports locale-scoped lookups and equality/prefix filters; it does not accelerate
        // ILIKE '%query%' contains patterns. Trigram (pg_trgm) is optional later if profiling warrants it.
        builder.HasIndex(localization => new { localization.Locale, localization.NormalizedName })
            .HasDatabaseName("IX_keyword_localizations_Locale_NormalizedName");
    }
}
