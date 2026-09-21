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

    private readonly Dictionary<(string MediaType, int TmdbId), ResolvedMovieIdentity?> _dedupCache = new();

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
            var cacheKey = (NormalizeMediaType(suggestion.MediaType), tmdbId);
            if (_dedupCache.TryGetValue(cacheKey, out var cached))
            {
                perfContext.RecordValidationDedupHit();
                return cached;
            }

            var resolved = await ResolveFromTmdbHintAsync(suggestion, tmdbId, cancellationToken);
            _dedupCache[cacheKey] = resolved;
            return resolved;
        }

        return await ResolveBySearchAsync(suggestion, cancellationToken);
    }

    private async Task<ResolvedMovieIdentity?> ResolveFromTmdbHintAsync(
        AiProviderSuggestion suggestion,
        int tmdbId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(suggestion.MediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            var catalogMovie = await movieRepository.GetByTmdbIdAsync(tmdbId, cancellationToken);
            if (catalogMovie is not null)
            {
                perfContext.RecordValidationCatalogHit();
                return AiResolvedIdentityMapper.FromMovie(catalogMovie);
            }

            return await ResolveMovieFromProviderAsync(suggestion, tmdbId, cancellationToken);
        }

        var catalogTvShow = await tvShowRepository.GetByTmdbIdAsync(tmdbId, cancellationToken);
        if (catalogTvShow is not null)
        {
            perfContext.RecordValidationCatalogHit();
            return AiResolvedIdentityMapper.FromTvShow(catalogTvShow);
        }

        return await ResolveTvShowFromProviderAsync(suggestion, tmdbId, cancellationToken);
    }

    private async Task<ResolvedMovieIdentity?> ResolveMovieFromProviderAsync(
        AiProviderSuggestion suggestion,
        int tmdbId,
        CancellationToken cancellationToken)
    {
        perfContext.RecordValidationProviderFallback();
        perfContext.RecordTmdbResolutionCall();

        var providerDetails = await movieDataProvider.GetMovieAsync(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            cancellationToken: cancellationToken);
        if (providerDetails is null)
        {
            return await ResolveMovieBySearchAsync(suggestion, cancellationToken);
        }

        var movie = await catalogProviderUpsertService.UpsertMovieFromProviderAsync(
            providerDetails,
            enrichKeywords: false,
            cancellationToken);

        return AiResolvedIdentityMapper.FromMovie(movie);
    }

    private async Task<ResolvedMovieIdentity?> ResolveTvShowFromProviderAsync(
        AiProviderSuggestion suggestion,
        int tmdbId,
        CancellationToken cancellationToken)
    {
        perfContext.RecordValidationProviderFallback();
        perfContext.RecordTmdbResolutionCall();

        var providerDetails = await tvShowDataProvider.GetTvShowAsync(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            cancellationToken: cancellationToken);
        if (providerDetails is null)
        {
            return await ResolveTvShowBySearchAsync(suggestion, cancellationToken);
        }

        var tvShow = await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
            providerDetails,
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
        if (match.TmdbId is int tmdbId && tmdbId > 0)
        {
            return await ResolveFromTmdbHintAsync(
                suggestion with
                {
                    TmdbId = tmdbId,
                    Title = match.Title,
                    Year = match.ReleaseDate?.Year ?? suggestion.Year
                },
                tmdbId,
                cancellationToken);
        }

        return null;
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
        if (match.TmdbId is int tmdbId && tmdbId > 0)
        {
            return await ResolveFromTmdbHintAsync(
                suggestion with
                {
                    TmdbId = tmdbId,
                    Title = match.Title,
                    Year = match.FirstAirDate?.Year ?? suggestion.Year
                },
                tmdbId,
                cancellationToken);
        }

        return null;
    }

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
