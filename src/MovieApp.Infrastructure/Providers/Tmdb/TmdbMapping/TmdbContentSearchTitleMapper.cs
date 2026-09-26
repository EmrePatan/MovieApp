using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbContentSearchTitleMapper
{
    internal static IReadOnlyList<ProviderSearchTitleEntry> MapMovie(TmdbMovieDetailsResponseJson details)
    {
        var entries = new List<ProviderSearchTitleEntry>();

        if (details.AlternativeTitles?.Titles is { Count: > 0 })
        {
            foreach (var alternative in details.AlternativeTitles.Titles)
            {
                AddAlternative(entries, alternative);
            }
        }

        if (details.Translations?.Translations is { Count: > 0 })
        {
            foreach (var translation in details.Translations.Translations)
            {
                AddMovieTranslation(entries, translation);
            }
        }

        return entries;
    }

    internal static IReadOnlyList<ProviderSearchTitleEntry> MapTvShow(TmdbTvDetailsResponseJson details)
    {
        var entries = new List<ProviderSearchTitleEntry>();

        if (details.AlternativeTitles?.Results is { Count: > 0 })
        {
            foreach (var alternative in details.AlternativeTitles.Results)
            {
                AddAlternative(entries, alternative);
            }
        }

        if (details.Translations?.Translations is { Count: > 0 })
        {
            foreach (var translation in details.Translations.Translations)
            {
                AddTvTranslation(entries, translation);
            }
        }

        return entries;
    }

    private static void AddAlternative(List<ProviderSearchTitleEntry> entries, TmdbAlternativeTitleJson alternative)
    {
        if (string.IsNullOrWhiteSpace(alternative.Title))
        {
            return;
        }

        entries.Add(new ProviderSearchTitleEntry(
            alternative.Title.Trim(),
            ContentSearchTitleKind.Alternative,
            ContentSearchTitleSource.TmdbAlternative,
            null,
            NormalizeCountry(alternative.Iso31661),
            NormalizeOptional(alternative.Type)));
    }

    private static void AddMovieTranslation(List<ProviderSearchTitleEntry> entries, TmdbTranslationJson translation)
    {
        var title = translation.Data?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        entries.Add(new ProviderSearchTitleEntry(
            title.Trim(),
            ContentSearchTitleKind.Translation,
            ContentSearchTitleSource.TmdbTranslation,
            NormalizeLanguage(translation.Iso6391),
            NormalizeCountry(translation.Iso31661),
            null));
    }

    private static void AddTvTranslation(List<ProviderSearchTitleEntry> entries, TmdbTvTranslationJson translation)
    {
        var title = translation.Data?.Name;
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        entries.Add(new ProviderSearchTitleEntry(
            title.Trim(),
            ContentSearchTitleKind.Translation,
            ContentSearchTitleSource.TmdbTranslation,
            NormalizeLanguage(translation.Iso6391),
            NormalizeCountry(translation.Iso31661),
            null));
    }

    private static string? NormalizeCountry(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeLanguage(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
