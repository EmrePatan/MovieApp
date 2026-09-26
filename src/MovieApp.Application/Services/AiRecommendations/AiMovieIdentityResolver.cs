using System.Globalization;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class AiMovieIdentityResolver(
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    IAiRecommendationPerfContext perfContext) : IMovieIdentityResolver
{
    private const int SearchPageSize = 10;

    private readonly Dictionary<(string MediaType, int TmdbId), MovieProviderDetails?> _movieDetailsCache = new();
    private readonly Dictionary<(string MediaType, int TmdbId), TvShowProviderDetails?> _tvShowDetailsCache = new();

    public async Task<ResolvedMovieIdentity?> ResolveAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupportedMediaType(suggestion.MediaType))
        {
            return null;
        }

        if (suggestion.TmdbId is int tmdbId && tmdbId > 0)
        {
            var resolved = await TryResolveValidatedTmdbIdAsync(suggestion, tmdbId, cancellationToken);
            if (resolved is not null)
            {
                return resolved;
            }

            return await ResolveBySearchAsync(ClearTmdbId(suggestion), cancellationToken);
        }

        return await ResolveBySearchAsync(suggestion, cancellationToken);
    }

    private async Task<ResolvedMovieIdentity?> TryResolveValidatedTmdbIdAsync(
        AiProviderSuggestion suggestion,
        int tmdbId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(suggestion.MediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            var details = await GetOrFetchMovieDetailsAsync(tmdbId, cancellationToken);
            if (details is null || !TmdbAiSuggestionIdentityValidator.MatchesMovie(suggestion, details))
            {
                return null;
            }

            return await MaterializeMovieAsync(details, cancellationToken);
        }

        var tvDetails = await GetOrFetchTvShowDetailsAsync(tmdbId, cancellationToken);
        if (tvDetails is null || !TmdbAiSuggestionIdentityValidator.MatchesTvShow(suggestion, tvDetails))
        {
            return null;
        }

        return await MaterializeTvShowAsync(tvDetails, cancellationToken);
    }

    private async Task<MovieProviderDetails?> GetOrFetchMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken)
    {
        var cacheKey = (NormalizeMediaType("movie"), tmdbId);
        if (_movieDetailsCache.TryGetValue(cacheKey, out var cached))
        {
            perfContext.RecordValidationDedupHit();
            return cached;
        }

        perfContext.RecordTmdbResolutionCall();

        var details = await movieDataProvider.GetMovieAsync(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            cancellationToken: cancellationToken);

        _movieDetailsCache[cacheKey] = details;
        return details;
    }

    private async Task<TvShowProviderDetails?> GetOrFetchTvShowDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken)
    {
        var cacheKey = (NormalizeMediaType("tv"), tmdbId);
        if (_tvShowDetailsCache.TryGetValue(cacheKey, out var cached))
        {
            perfContext.RecordValidationDedupHit();
            return cached;
        }

        perfContext.RecordTmdbResolutionCall();

        var details = await tvShowDataProvider.GetTvShowAsync(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            cancellationToken: cancellationToken);

        _tvShowDetailsCache[cacheKey] = details;
        return details;
    }

    private async Task<ResolvedMovieIdentity?> MaterializeMovieAsync(
        MovieProviderDetails details,
        CancellationToken cancellationToken)
    {
        if (details.TmdbId is not int tmdbId)
        {
            return null;
        }

        var catalogMovie = await movieRepository.GetByTmdbIdAsync(tmdbId, cancellationToken);
        if (catalogMovie is not null)
        {
            perfContext.RecordValidationCatalogHit();
            return AiResolvedIdentityMapper.FromMovie(catalogMovie);
        }

        perfContext.RecordValidationProviderFallback();

        var movie = await catalogProviderUpsertService.UpsertMovieFromProviderAsync(
            details,
            enrichKeywords: false,
            cancellationToken);

        return AiResolvedIdentityMapper.FromMovie(movie);
    }

    private async Task<ResolvedMovieIdentity?> MaterializeTvShowAsync(
        TvShowProviderDetails details,
        CancellationToken cancellationToken)
    {
        if (details.TmdbId is not int tmdbId)
        {
            return null;
        }

        var catalogTvShow = await tvShowRepository.GetByTmdbIdAsync(tmdbId, cancellationToken);
        if (catalogTvShow is not null)
        {
            perfContext.RecordValidationCatalogHit();
            return AiResolvedIdentityMapper.FromTvShow(catalogTvShow);
        }

        perfContext.RecordValidationProviderFallback();

        var tvShow = await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
            details,
            enrichKeywords: false,
            cancellationToken);

        return AiResolvedIdentityMapper.FromTvShow(tvShow);
    }

    private async Task<ResolvedMovieIdentity?> ResolveBySearchAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        if (string.Equals(suggestion.MediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveMovieBySearchAsync(suggestion, cancellationToken);
        }

        return await ResolveTvShowBySearchAsync(suggestion, cancellationToken);
    }

    private async Task<ResolvedMovieIdentity?> ResolveMovieBySearchAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        perfContext.RecordValidationSearchFallback();
        perfContext.RecordTmdbResolutionCall();

        var query = BuildSearchQuery(suggestion);
        var searchResult = await movieDataProvider.SearchMoviesAsync(query, 1, SearchPageSize, cancellationToken);
        var candidates = searchResult.Results
            .Where(item => TitleYearMatcher.MatchesSearchFallback(
                item.Title,
                item.OriginalTitle,
                suggestion.Title,
                suggestion.Year,
                item.ReleaseDate))
            .ToList();

        if (candidates.Count != 1)
        {
            return null;
        }

        var match = candidates[0];
        if (match.TmdbId is not int matchTmdbId || matchTmdbId <= 0)
        {
            return null;
        }

        return await TryResolveValidatedTmdbIdAsync(suggestion, matchTmdbId, cancellationToken);
    }

    private async Task<ResolvedMovieIdentity?> ResolveTvShowBySearchAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        perfContext.RecordValidationSearchFallback();
        perfContext.RecordTmdbResolutionCall();

        var query = BuildSearchQuery(suggestion);
        var searchResult = await tvShowDataProvider.SearchTvShowsAsync(query, 1, SearchPageSize, cancellationToken);
        var candidates = searchResult.Results
            .Where(item => TitleYearMatcher.MatchesSearchFallback(
                item.Title,
                item.OriginalTitle,
                suggestion.Title,
                suggestion.Year,
                item.FirstAirDate))
            .ToList();

        if (candidates.Count != 1)
        {
            return null;
        }

        var match = candidates[0];
        if (match.TmdbId is not int matchTmdbId || matchTmdbId <= 0)
        {
            return null;
        }

        return await TryResolveValidatedTmdbIdAsync(suggestion, matchTmdbId, cancellationToken);
    }

    private static AiProviderSuggestion ClearTmdbId(AiProviderSuggestion suggestion) =>
        suggestion with { TmdbId = null };

    private static string BuildSearchQuery(AiProviderSuggestion suggestion) =>
        suggestion.Year > 0
            ? $"{suggestion.Title} {suggestion.Year}"
            : suggestion.Title;

    private static bool IsSupportedMediaType(string mediaType) =>
        string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeMediaType(string mediaType) =>
        string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie";
}
