using MovieApp.Application.Models.Images;

namespace MovieApp.Application.Services.Localization;

public static class LocalizedPosterSelector
{
    private const decimal MinimumPosterWidth = 200m;
    private const decimal MinimumPosterAspectRatio = 0.55m;
    private const decimal MaximumPosterAspectRatio = 0.85m;

    public readonly record struct Selection(string? PosterPath, bool ShouldPersist);

    public static Selection Select(
        IReadOnlyList<ProviderImageResult> posters,
        string languageKey,
        string? canonicalPosterPath)
    {
        if (posters.Count == 0)
        {
            return new Selection(canonicalPosterPath, ShouldPersist: false);
        }

        var normalizedLanguage = NormalizeLanguageKey(languageKey);

        var languageMatch = SelectBestInTier(
            posters,
            image => string.Equals(NormalizeLanguageKey(image.Language), normalizedLanguage, StringComparison.Ordinal));
        if (languageMatch is not null)
        {
            return new Selection(languageMatch.FilePath, ShouldPersist: true);
        }

        var neutralMatch = SelectBestInTier(
            posters,
            image => string.IsNullOrWhiteSpace(image.Language));
        if (neutralMatch is not null)
        {
            return new Selection(neutralMatch.FilePath, ShouldPersist: true);
        }

        return new Selection(canonicalPosterPath, ShouldPersist: false);
    }

    private static ProviderImageResult? SelectBestInTier(
        IReadOnlyList<ProviderImageResult> posters,
        Func<ProviderImageResult, bool> predicate)
    {
        return posters
            .Where(image => !string.IsNullOrWhiteSpace(image.FilePath))
            .Where(IsCardSuitablePoster)
            .Where(predicate)
            .OrderByDescending(image => image.VoteAverage)
            .ThenByDescending(image => image.VoteCount)
            .ThenBy(image => image.FilePath, StringComparer.Ordinal)
            .FirstOrDefault();
    }

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
