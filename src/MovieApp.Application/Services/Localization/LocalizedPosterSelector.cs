using MovieApp.Application.Models.Images;

namespace MovieApp.Application.Services.Localization;

public static class LocalizedPosterSelector
{
    private const decimal MinimumPosterWidth = 200m;
    private const decimal MinimumPosterAspectRatio = 0.55m;
    private const decimal MaximumPosterAspectRatio = 0.85m;
    private const int MinimumTrustedVoteCount = 5;

    public readonly record struct Selection(string? PosterPath, bool ShouldPersist);

    public static Selection Select(
        IReadOnlyList<ProviderImageResult> posters,
        string languageKey,
        string? canonicalPosterPath,
        string? originalLanguage = null,
        string? primaryOriginCountryCode = null)
    {
        if (posters.Count == 0)
        {
            return new Selection(canonicalPosterPath, ShouldPersist: false);
        }

        var normalizedLanguage = NormalizeLanguageKey(languageKey);
        var isTurkishArtworkRequest = string.Equals(
            normalizedLanguage,
            SupportedArtworkLanguageKeys.Turkish,
            StringComparison.Ordinal);
        var isTurkishProduction = ContentProductionHeuristics.IsTurkishProduction(
            originalLanguage,
            primaryOriginCountryCode);

        if (isTurkishArtworkRequest && !isTurkishProduction)
        {
            return new Selection(canonicalPosterPath, ShouldPersist: false);
        }

        var languageMatch = SelectBestInTier(
            posters,
            image => string.Equals(NormalizeLanguageKey(image.Language), normalizedLanguage, StringComparison.Ordinal),
            canonicalPosterPath);
        if (languageMatch is not null)
        {
            if (isTurkishArtworkRequest && !isTurkishProduction)
            {
                return new Selection(canonicalPosterPath, ShouldPersist: false);
            }

            return new Selection(languageMatch.FilePath, ShouldPersist: true);
        }

        if (isTurkishProduction)
        {
            var neutralMatch = SelectBestInTier(
                posters,
                image => string.IsNullOrWhiteSpace(image.Language),
                canonicalPosterPath);
            if (neutralMatch is not null)
            {
                return new Selection(neutralMatch.FilePath, ShouldPersist: true);
            }
        }

        return new Selection(canonicalPosterPath, ShouldPersist: false);
    }

    internal static bool ShouldUseStoredLocalizedPoster(
        string? canonicalPosterPath,
        string localizedPosterPath,
        string contentLocale,
        string? originalLanguage,
        string? primaryOriginCountryCode = null)
    {
        if (string.Equals(localizedPosterPath, canonicalPosterPath, StringComparison.Ordinal))
        {
            return true;
        }

        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return true;
        }

        if (!string.Equals(
                SupportedArtworkLanguageKeys.ResolvePosterLanguageKey(contentLocale),
                SupportedArtworkLanguageKeys.Turkish,
                StringComparison.Ordinal))
        {
            return true;
        }

        return ContentProductionHeuristics.IsTurkishProduction(originalLanguage, primaryOriginCountryCode);
    }

    private static ProviderImageResult? SelectBestInTier(
        IReadOnlyList<ProviderImageResult> posters,
        Func<ProviderImageResult, bool> predicate,
        string? canonicalPosterPath)
    {
        return posters
            .Where(image => !string.IsNullOrWhiteSpace(image.FilePath))
            .Where(IsCardSuitablePoster)
            .Where(predicate)
            .OrderByDescending(image => MatchesCanonicalPoster(image, canonicalPosterPath))
            .ThenByDescending(image => image.VoteCount >= MinimumTrustedVoteCount)
            .ThenByDescending(image => image.VoteAverage)
            .ThenByDescending(image => image.VoteCount)
            .ThenBy(image => image.FilePath, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool MatchesCanonicalPoster(ProviderImageResult image, string? canonicalPosterPath) =>
        !string.IsNullOrWhiteSpace(canonicalPosterPath) &&
        string.Equals(image.FilePath, canonicalPosterPath, StringComparison.Ordinal);

    internal static bool IsCardSuitablePoster(ProviderImageResult image)
    {
        if (image.Width > 0 && image.Width < MinimumPosterWidth)
        {
            return false;
        }

        if (image.AspectRatio > 0m &&
            (image.AspectRatio < MinimumPosterAspectRatio || image.AspectRatio > MaximumPosterAspectRatio))
        {
            return false;
        }

        return true;
    }

    private static string? NormalizeLanguageKey(string? language) =>
        string.IsNullOrWhiteSpace(language)
            ? null
            : language.Trim().ToLowerInvariant();
}
