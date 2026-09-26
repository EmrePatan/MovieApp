using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.Application.Services.Localization;

public static class LocalizedPosterDisplayOverlay
{
    public static async Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> LoadPosterPathsAsync(
        IContentLocalizedPosterRepository repository,
        IReadOnlyList<ContentLocalizedPosterKey> keys,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var languageKey = SupportedArtworkLanguageKeys.ResolvePosterLanguageKey(contentLocale);
        if (languageKey is null || keys.Count == 0)
        {
            return new Dictionary<ContentLocalizedPosterKey, string>();
        }

        return await repository.GetPosterPathsAsync(keys, languageKey, cancellationToken);
    }

    public static string? ChooseDisplayPosterUrl(
        string? canonicalPosterUrl,
        ContentLocalizedPosterKey key,
        IReadOnlyDictionary<ContentLocalizedPosterKey, string> localizedPosters) =>
        localizedPosters.TryGetValue(key, out var localizedPoster) && !string.IsNullOrWhiteSpace(localizedPoster)
            ? localizedPoster
            : canonicalPosterUrl;
}
