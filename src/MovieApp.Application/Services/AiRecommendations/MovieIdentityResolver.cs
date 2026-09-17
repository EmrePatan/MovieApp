using System.Globalization;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Movies;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class MovieIdentityResolver(
    IGetMovieByTmdbIdService getMovieByTmdbIdService,
    ISearchMoviesService searchMoviesService) : IMovieIdentityResolver
{
    public async Task<ResolvedMovieIdentity?> ResolveAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(suggestion.MediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (suggestion.TmdbId is int tmdbId && tmdbId > 0)
        {
            var fromTmdb = await TryResolveFromTmdbHintAsync(tmdbId, suggestion, cancellationToken);
            if (fromTmdb is not null)
            {
                return fromTmdb;
            }
        }

        return await ResolveByTitleAndYearAsync(suggestion, cancellationToken);
    }

    private async Task<ResolvedMovieIdentity?> TryResolveFromTmdbHintAsync(
        int tmdbId,
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        try
        {
            var details = await getMovieByTmdbIdService.GetAsync(tmdbId, cancellationToken);
            if (!TitleYearMatcher.Matches(details.Title, details.OriginalTitle, suggestion.Title, suggestion.Year, details.ReleaseDate))
            {
                return null;
            }

            return ToResolvedIdentity(details);
        }
        catch (NotFoundException)
        {
            return null;
        }
        catch (ValidationException)
        {
            return null;
        }
    }

    private async Task<ResolvedMovieIdentity?> ResolveByTitleAndYearAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        var query = suggestion.Year > 0
            ? $"{suggestion.Title} {suggestion.Year}"
            : suggestion.Title;

        var searchResult = await searchMoviesService.SearchAsync(
            new MovieSearchRequest(query, 1, 10),
            cancellationToken);

        var candidates = searchResult.Items
            .Where(item => TitleYearMatcher.Matches(item.Title, null, suggestion.Title, suggestion.Year, item.ReleaseDate))
            .ToList();

        if (candidates.Count != 1)
        {
            return null;
        }

        var match = candidates[0];
        if (match.TmdbId is int tmdbId && tmdbId > 0)
        {
            try
            {
                var details = await getMovieByTmdbIdService.GetAsync(tmdbId, cancellationToken);
                return ToResolvedIdentity(details);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        return null;
    }

    private static ResolvedMovieIdentity ToResolvedIdentity(MovieDetailsResult details) =>
        new(
            details.Id,
            details.TmdbId,
            details.Title,
            details.ReleaseDate?.Year,
            details.RuntimeMinutes,
            details.OriginalTitle,
            details.Overview,
            details.PosterPath,
            details.BackdropPath,
            details.ReleaseDate,
            details.VoteAverage,
            details.VoteCount,
            details.Genres);
}

internal static class TitleYearMatcher
{
    internal static bool Matches(
        string? candidateTitle,
        string? candidateOriginalTitle,
        string suggestionTitle,
        int suggestionYear,
        DateOnly? releaseDate)
    {
        if (!TitleMatches(candidateTitle, candidateOriginalTitle, suggestionTitle))
        {
            return false;
        }

        if (suggestionYear <= 0)
        {
            return true;
        }

        return releaseDate?.Year == suggestionYear;
    }

    private static bool TitleMatches(string? candidateTitle, string? candidateOriginalTitle, string suggestionTitle)
    {
        var normalizedSuggestion = NormalizeTitle(suggestionTitle);
        if (string.IsNullOrEmpty(normalizedSuggestion))
        {
            return false;
        }

        if (NormalizeTitle(candidateTitle) == normalizedSuggestion)
        {
            return true;
        }

        return NormalizeTitle(candidateOriginalTitle) == normalizedSuggestion;
    }

    private static string NormalizeTitle(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : title.Trim().ToLower(CultureInfo.InvariantCulture);
}
