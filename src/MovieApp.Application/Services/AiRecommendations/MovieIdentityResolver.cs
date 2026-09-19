using System.Globalization;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.TvShows;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class MovieIdentityResolver(
    IGetMovieByTmdbIdService getMovieByTmdbIdService,
    ISearchMoviesService searchMoviesService,
    IGetTvShowByTmdbIdService getTvShowByTmdbIdService,
    ISearchTvShowsService searchTvShowsService,
    IAiRecommendationPerfContext perfContext) : IMovieIdentityResolver
{
    public async Task<ResolvedMovieIdentity?> ResolveAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(suggestion.MediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveMovieAsync(suggestion, cancellationToken);
        }

        if (string.Equals(suggestion.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveTvShowAsync(suggestion, cancellationToken);
        }

        return null;
    }

    private async Task<ResolvedMovieIdentity?> ResolveMovieAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        if (suggestion.TmdbId is int tmdbId && tmdbId > 0)
        {
            var fromTmdb = await TryResolveMovieFromTmdbHintAsync(tmdbId, suggestion, cancellationToken);
            if (fromTmdb is not null)
            {
                return fromTmdb;
            }
        }

        return await ResolveMovieByTitleAndYearAsync(suggestion, cancellationToken);
    }

    private async Task<ResolvedMovieIdentity?> ResolveTvShowAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        if (suggestion.TmdbId is int tmdbId && tmdbId > 0)
        {
            var fromTmdb = await TryResolveTvShowFromTmdbHintAsync(tmdbId, suggestion, cancellationToken);
            if (fromTmdb is not null)
            {
                return fromTmdb;
            }
        }

        return await ResolveTvShowByTitleAndYearAsync(suggestion, cancellationToken);
    }

    private async Task<ResolvedMovieIdentity?> TryResolveMovieFromTmdbHintAsync(
        int tmdbId,
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        try
        {
            perfContext.RecordTmdbResolutionCall();
            var details = await getMovieByTmdbIdService.GetAsync(tmdbId, cancellationToken);
            if (!TitleYearMatcher.Matches(details.Title, details.OriginalTitle, suggestion.Title, suggestion.Year, details.ReleaseDate))
            {
                return null;
            }

            return ToResolvedMovieIdentity(details);
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

    private async Task<ResolvedMovieIdentity?> TryResolveTvShowFromTmdbHintAsync(
        int tmdbId,
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        try
        {
            perfContext.RecordTmdbResolutionCall();
            var details = await getTvShowByTmdbIdService.GetAsync(tmdbId, cancellationToken);
            if (!TitleYearMatcher.Matches(details.Title, details.OriginalTitle, suggestion.Title, suggestion.Year, details.FirstAirDate))
            {
                return null;
            }

            return ToResolvedTvShowIdentity(details);
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

    private async Task<ResolvedMovieIdentity?> ResolveMovieByTitleAndYearAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        var query = suggestion.Year > 0
            ? $"{suggestion.Title} {suggestion.Year}"
            : suggestion.Title;

        perfContext.RecordTmdbResolutionCall();
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
                perfContext.RecordTmdbResolutionCall();
                var details = await getMovieByTmdbIdService.GetAsync(tmdbId, cancellationToken);
                return ToResolvedMovieIdentity(details);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        return null;
    }

    private async Task<ResolvedMovieIdentity?> ResolveTvShowByTitleAndYearAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        var query = suggestion.Year > 0
            ? $"{suggestion.Title} {suggestion.Year}"
            : suggestion.Title;

        perfContext.RecordTmdbResolutionCall();
        var searchResult = await searchTvShowsService.SearchAsync(
            new TvShowSearchRequest(query, 1, 10),
            cancellationToken);

        var candidates = searchResult.Items
            .Where(item => TitleYearMatcher.Matches(item.Title, item.OriginalTitle, suggestion.Title, suggestion.Year, item.FirstAirDate))
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
                perfContext.RecordTmdbResolutionCall();
                var details = await getTvShowByTmdbIdService.GetAsync(tmdbId, cancellationToken);
                return ToResolvedTvShowIdentity(details);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        return null;
    }

    private static ResolvedMovieIdentity ToResolvedMovieIdentity(MovieDetailsResult details) =>
        new(
            "movie",
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

    private static ResolvedMovieIdentity ToResolvedTvShowIdentity(TvShowDetailsResult details) =>
        new(
            "tv",
            details.Id,
            details.TmdbId,
            details.Title,
            details.FirstAirDate?.Year,
            null,
            details.OriginalTitle,
            details.Overview,
            details.PosterPath,
            details.BackdropPath,
            details.FirstAirDate,
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
