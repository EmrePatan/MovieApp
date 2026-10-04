using MovieApp.Application.Common;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Common;

public sealed class ContentSearchTitleMetadataRepairSelectionTests
{
    private static readonly DateTime ProviderSyncedAt = new(2026, 4, 4, 1, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ContentSearchTitleKind.Translation, ContentSearchTitleSource.TmdbTranslation, null, "TR", null, true)]
    [InlineData(ContentSearchTitleKind.Translation, ContentSearchTitleSource.TmdbTranslation, "", "TR", null, true)]
    [InlineData(ContentSearchTitleKind.Translation, ContentSearchTitleSource.TmdbTranslation, "tr", "TR", null, false)]
    [InlineData(ContentSearchTitleKind.Alternative, ContentSearchTitleSource.TmdbAlternative, null, null, null, true)]
    [InlineData(ContentSearchTitleKind.Alternative, ContentSearchTitleSource.TmdbAlternative, null, "", null, true)]
    [InlineData(ContentSearchTitleKind.Alternative, ContentSearchTitleSource.TmdbAlternative, null, "US", null, false)]
    [InlineData(ContentSearchTitleKind.Canonical, ContentSearchTitleSource.CatalogCanonical, null, null, null, false)]
    [InlineData(ContentSearchTitleKind.Translation, ContentSearchTitleSource.TmdbAlternative, null, null, null, false)]
    public void RowNeedsRepair_MatchesProviderSearchTitleMetadataRules(
        ContentSearchTitleKind titleKind,
        ContentSearchTitleSource source,
        string? languageCode,
        string? countryCode,
        DateTime? providerUpdatedAtUtc,
        bool expected) =>
        Assert.Equal(
            expected,
            ContentSearchTitleMetadataRepairSelection.RowNeedsRepair(
                titleKind,
                source,
                languageCode,
                countryCode,
                providerUpdatedAtUtc));

    [Fact]
    public void RowNeedsRepair_StaleAlternativeIsSelectedUntilProviderSync()
    {
        Assert.True(ContentSearchTitleMetadataRepairSelection.RowNeedsRepair(
            ContentSearchTitleKind.Alternative,
            ContentSearchTitleSource.TmdbAlternative,
            null,
            null,
            providerUpdatedAtUtc: null));
    }

    [Fact]
    public void RowNeedsRepair_EnrichedAlternativeWithInvalidProviderCountry_DoesNotLoop()
    {
        Assert.False(ContentSearchTitleMetadataRepairSelection.RowNeedsRepair(
            ContentSearchTitleKind.Alternative,
            ContentSearchTitleSource.TmdbAlternative,
            null,
            null,
            ProviderSyncedAt));
    }

    [Fact]
    public void RowNeedsRepair_EnrichedTranslationWithInvalidProviderLanguage_DoesNotLoop()
    {
        Assert.False(ContentSearchTitleMetadataRepairSelection.RowNeedsRepair(
            ContentSearchTitleKind.Translation,
            ContentSearchTitleSource.TmdbTranslation,
            null,
            "TR",
            ProviderSyncedAt));
    }
}
