using System.Globalization;
using System.Text;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class AiRequestedPersonFilmographyResolver(
    IPersonDataProvider personDataProvider,
    IAiRecommendationPerfContext perfContext) : IAiRequestedPersonFilmographyResolver
{
    private const int PersonSearchPageSize = 5;

    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<PersonFilmographyConstraint?> TryResolveAsync(
        string userMessage,
        string? searchLanguage = null,
        CancellationToken cancellationToken = default)
    {
        var personQuery = AiRequestedPersonSearchQueryExtractor.TryExtract(userMessage);
        if (personQuery is null)
        {
            return null;
        }

        perfContext.RecordTmdbResolutionCall();
        var searchResult = await personDataProvider.SearchPersonsAsync(
            personQuery,
            1,
            PersonSearchPageSize,
            cancellationToken);

        var personSummary = PickBestPersonMatch(personQuery, searchResult.Results);
        if (personSummary is null)
        {
            return null;
        }

        perfContext.RecordTmdbResolutionCall();
        var personDetails = await personDataProvider.GetPersonAsync(personSummary.TmdbId, cancellationToken);
        if (personDetails is null || personDetails.FilmographyCredits.Count == 0)
        {
            return null;
        }

        var creditKeys = new HashSet<PersonFilmographyCreditKey>();
        foreach (var credit in personDetails.FilmographyCredits)
        {
            if (credit.TmdbId <= 0)
            {
                continue;
            }

            var mediaType = string.Equals(credit.MediaType, "tv", StringComparison.OrdinalIgnoreCase)
                ? "tv"
                : "movie";

            creditKeys.Add(new PersonFilmographyCreditKey(mediaType, credit.TmdbId));
        }

        if (creditKeys.Count == 0)
        {
            return null;
        }

        return new PersonFilmographyConstraint(
            personDetails.TmdbId,
            personDetails.Name,
            creditKeys);
    }

    private static PersonProviderSummary? PickBestPersonMatch(
        string query,
        IReadOnlyList<PersonProviderSummary> results)
    {
        if (results.Count == 0)
        {
            return null;
        }

        var normalizedQuery = NormalizePersonName(query);

        var exactMatches = results
            .Where(result => NormalizePersonName(result.Name) == normalizedQuery)
            .ToList();

        if (exactMatches.Count == 1)
        {
            return exactMatches[0];
        }

        if (exactMatches.Count > 1)
        {
            return exactMatches
                .OrderByDescending(result => result.Popularity)
                .First();
        }

        var partialMatches = results
            .Where(result =>
            {
                var normalizedName = NormalizePersonName(result.Name);
                return normalizedName.Contains(normalizedQuery, StringComparison.Ordinal) ||
                       normalizedQuery.Contains(normalizedName, StringComparison.Ordinal);
            })
            .ToList();

        if (partialMatches.Count == 1)
        {
            return partialMatches[0];
        }

        if (partialMatches.Count > 1)
        {
            return partialMatches
                .OrderByDescending(result => result.Popularity)
                .First();
        }

        if (results.Count == 1)
        {
            return results[0];
        }

        return null;
    }

    private static string NormalizePersonName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lower = value.Trim().ToLower(TurkishCulture);
        var builder = new StringBuilder(lower.Length);
        foreach (var character in lower.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character);
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("ı", "i", StringComparison.Ordinal);
    }
}
