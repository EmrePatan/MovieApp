using MovieApp.Domain.Enums;

namespace MovieApp.Application.Common;

public static class ContentSearchTitleMetadataRepairSelection
{
    public static bool RowNeedsRepair(
        ContentSearchTitleKind titleKind,
        ContentSearchTitleSource source,
        string? languageCode,
        string? countryCode,
        DateTime? providerUpdatedAtUtc = null)
    {
        if (titleKind == ContentSearchTitleKind.Translation
            && source == ContentSearchTitleSource.TmdbTranslation)
        {
            return string.IsNullOrEmpty(languageCode)
                && providerUpdatedAtUtc is null;
        }

        if (titleKind == ContentSearchTitleKind.Alternative
            && source == ContentSearchTitleSource.TmdbAlternative)
        {
            return string.IsNullOrEmpty(countryCode)
                && providerUpdatedAtUtc is null;
        }

        return false;
    }
}
