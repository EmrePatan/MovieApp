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
    IAiRecommendationTmdbSearch tmdbSearch,
    IAiRecommendationPerfContext perfContext) : IMovieIdentityResolver
{
    private const int SearchPageSize = 10;

    private readonly Dictionary<(string MediaType, int TmdbId), MovieProviderDetails?> _movieDetailsCache = new();
    private readonly Dictionary<(string MediaType, int TmdbId), TvShowProviderDetails?> _tvShowDetailsCache = new();

    public async Task<ResolvedMovieIdentity?> ResolveAsync(
        AiProviderSuggestion suggestion,
        string? searchLanguage = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupportedMediaType(suggestion.MediaType))
        {
            return null;
        }

        var tmdbLanguage = AiRecommendationSearchLanguage.ToTmdbLanguage(searchLanguage);

        var primary = await ResolveInternalAsync(suggestion, tmdbLanguage, cancellationToken);
        if (primary.Identity is not null)
        {
            return primary.Identity;
        }

        if (primary.SearchWasAmbiguous)
        {
            return null;
        }

        var alternateMediaType = GetAlternateMediaType(suggestion.MediaType);
        if (alternateMediaType is null)
        {
            return null;
        }

        var alternate = await ResolveInternalAsync(
            ClearTmdbId(suggestion with { MediaType = alternateMediaType }),
            tmdbLanguage,
            cancellationToken);

        return alternate.Identity;
    }

    private async Task<ResolveInternalResult> ResolveInternalAsync(
        AiProviderSuggestion suggestion,
        string? tmdbLanguage,
        CancellationToken cancellationToken)
    {
        if (suggestion.TmdbId is int tmdbId && tmdbId > 0)
        {
            var resolved = await TryResolveValidatedTmdbIdAsync(suggestion, tmdbId, cancellationToken);
            if (resolved is not null)
            {
                return ResolveInternalResult.Success(resolved);
            }

            return await ResolveBySearchAsync(ClearTmdbId(suggestion), tmdbLanguage, cancellationToken);
        }

        return await ResolveBySearchAsync(suggestion, tmdbLanguage, cancellationToken);
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

    private async Task<ResolveInternalResult> ResolveBySearchAsync(
        AiProviderSuggestion suggestion,
        string? tmdbLanguage,
        CancellationToken cancellationToken)
    {
        if (string.Equals(suggestion.MediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveMovieBySearchAsync(suggestion, tmdbLanguage, cancellationToken);
        }

        return await ResolveTvShowBySearchAsync(suggestion, tmdbLanguage, cancellationToken);
    }

    private async Task<ResolveInternalResult> ResolveMovieBySearchAsync(
        AiProviderSuggestion suggestion,
        string? tmdbLanguage,
        CancellationToken cancellationToken)
    {
        var firstAttempt = await TryResolveMovieBySearchQueryAsync(
            suggestion,
            BuildSearchQuery(suggestion),
            tmdbLanguage,
            cancellationToken);

        if (firstAttempt.Status is SearchResolveAttemptStatus.Success)
        {
            return ResolveInternalResult.Success(firstAttempt.Identity);
        }

        if (firstAttempt.Status is SearchResolveAttemptStatus.Ambiguous)
        {
            return ResolveInternalResult.Ambiguous();
        }

        if (suggestion.Year <= 0)
        {
            return ResolveInternalResult.Failure();
        }

        var secondAttempt = await TryResolveMovieBySearchQueryAsync(
            suggestion,
            suggestion.Title,
            tmdbLanguage,
            cancellationToken);

        if (secondAttempt.Status is SearchResolveAttemptStatus.Success)
        {
            return ResolveInternalResult.Success(secondAttempt.Identity);
        }

        if (secondAttempt.Status is SearchResolveAttemptStatus.Ambiguous)
        {
            return ResolveInternalResult.Ambiguous();
        }

        return ResolveInternalResult.Failure();
    }

    private async Task<ResolveInternalResult> ResolveTvShowBySearchAsync(
        AiProviderSuggestion suggestion,
        string? tmdbLanguage,
        CancellationToken cancellationToken)
    {
        var firstAttempt = await TryResolveTvShowBySearchQueryAsync(
            suggestion,
            BuildSearchQuery(suggestion),
            tmdbLanguage,
            cancellationToken);

        if (firstAttempt.Status is SearchResolveAttemptStatus.Success)
        {
            return ResolveInternalResult.Success(firstAttempt.Identity);
        }

        if (firstAttempt.Status is SearchResolveAttemptStatus.Ambiguous)
        {
            return ResolveInternalResult.Ambiguous();
        }

        if (suggestion.Year <= 0)
        {
            return ResolveInternalResult.Failure();
        }

        var secondAttempt = await TryResolveTvShowBySearchQueryAsync(
            suggestion,
            suggestion.Title,
            tmdbLanguage,
            cancellationToken);

        if (secondAttempt.Status is SearchResolveAttemptStatus.Success)
        {
            return ResolveInternalResult.Success(secondAttempt.Identity);
        }

        if (secondAttempt.Status is SearchResolveAttemptStatus.Ambiguous)
        {
            return ResolveInternalResult.Ambiguous();
        }

        return ResolveInternalResult.Failure();
    }

    private async Task<SearchResolveAttempt> TryResolveMovieBySearchQueryAsync(
        AiProviderSuggestion suggestion,
        string query,
        string? tmdbLanguage,
        CancellationToken cancellationToken)
    {
        perfContext.RecordValidationSearchFallback();
        perfContext.RecordTmdbResolutionCall();

        var searchResult = await tmdbSearch.SearchMoviesAsync(
            query,
            1,
            SearchPageSize,
            tmdbLanguage,
            cancellationToken);

        return await ResolveSingleMovieFromSearchResultsAsync(suggestion, searchResult.Results, cancellationToken);
    }

    private async Task<SearchResolveAttempt> TryResolveTvShowBySearchQueryAsync(
        AiProviderSuggestion suggestion,
        string query,
        string? tmdbLanguage,
        CancellationToken cancellationToken)
    {
        perfContext.RecordValidationSearchFallback();
        perfContext.RecordTmdbResolutionCall();

        var searchResult = await tmdbSearch.SearchTvShowsAsync(
            query,
            1,
            SearchPageSize,
            tmdbLanguage,
            cancellationToken);

        return await ResolveSingleTvShowFromSearchResultsAsync(suggestion, searchResult.Results, cancellationToken);
    }

    private async Task<SearchResolveAttempt> ResolveSingleMovieFromSearchResultsAsync(
        AiProviderSuggestion suggestion,
        IReadOnlyList<MovieProviderSummary> searchResults,
        CancellationToken cancellationToken)
    {
        MovieProviderDetails? matchedDetails = null;

        foreach (var item in searchResults)
        {
            if (item.TmdbId is not int tmdbId || tmdbId <= 0)
            {
                continue;
            }

            if (!SearchSummaryYearIsCompatible(suggestion.Year, item.ReleaseDate))
            {
                continue;
            }

            var details = await GetOrFetchMovieDetailsAsync(tmdbId, cancellationToken);
            if (details is null || !TmdbAiSuggestionIdentityValidator.MatchesMovie(suggestion, details))
            {
                continue;
            }

            if (matchedDetails is not null)
            {
                return SearchResolveAttempt.Ambiguous();
            }

            matchedDetails = details;
        }

        return matchedDetails is null
            ? SearchResolveAttempt.NoMatch()
            : SearchResolveAttempt.Success(await MaterializeMovieAsync(matchedDetails, cancellationToken));
    }

    private async Task<SearchResolveAttempt> ResolveSingleTvShowFromSearchResultsAsync(
        AiProviderSuggestion suggestion,
        IReadOnlyList<TvShowProviderSummary> searchResults,
        CancellationToken cancellationToken)
    {
        TvShowProviderDetails? matchedDetails = null;

        foreach (var item in searchResults)
        {
            if (item.TmdbId is not int tmdbId || tmdbId <= 0)
            {
                continue;
            }

            if (!SearchSummaryYearIsCompatible(suggestion.Year, item.FirstAirDate))
            {
                continue;
            }

            var details = await GetOrFetchTvShowDetailsAsync(tmdbId, cancellationToken);
            if (details is null || !TmdbAiSuggestionIdentityValidator.MatchesTvShow(suggestion, details))
            {
                continue;
            }

            if (matchedDetails is not null)
            {
                return SearchResolveAttempt.Ambiguous();
            }

            matchedDetails = details;
        }

        return matchedDetails is null
            ? SearchResolveAttempt.NoMatch()
            : SearchResolveAttempt.Success(await MaterializeTvShowAsync(matchedDetails, cancellationToken));
    }

    private static bool SearchSummaryYearIsCompatible(int suggestionYear, DateOnly? releaseDate)
    {
        if (suggestionYear <= 0)
        {
            return true;
        }

        if (releaseDate is null)
        {
            return true;
        }

        return Math.Abs(releaseDate.Value.Year - suggestionYear) <= 1;
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

    private static string? GetAlternateMediaType(string mediaType)
    {
        if (string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return "tv";
        }

        if (string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            return "movie";
        }

        return null;
    }

    private static string NormalizeMediaType(string mediaType) =>
        string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie";

    private enum SearchResolveAttemptStatus
    {
        NoMatch,
        Success,
        Ambiguous
    }

    private readonly struct ResolveInternalResult
    {
        private ResolveInternalResult(ResolvedMovieIdentity? identity, bool searchWasAmbiguous)
        {
            Identity = identity;
            SearchWasAmbiguous = searchWasAmbiguous;
        }

        public ResolvedMovieIdentity? Identity { get; }

        public bool SearchWasAmbiguous { get; }

        public static ResolveInternalResult Success(ResolvedMovieIdentity? identity) =>
            new(identity, false);

        public static ResolveInternalResult Failure() =>
            new(null, false);

        public static ResolveInternalResult Ambiguous() =>
            new(null, true);
    }

    private readonly struct SearchResolveAttempt
    {
        private SearchResolveAttempt(SearchResolveAttemptStatus status, ResolvedMovieIdentity? identity)
        {
            Status = status;
            Identity = identity;
        }

        public SearchResolveAttemptStatus Status { get; }

        public ResolvedMovieIdentity? Identity { get; }

        public static SearchResolveAttempt NoMatch() =>
            new(SearchResolveAttemptStatus.NoMatch, null);

        public static SearchResolveAttempt Ambiguous() =>
            new(SearchResolveAttemptStatus.Ambiguous, null);

        public static SearchResolveAttempt Success(ResolvedMovieIdentity? identity) =>
            new(SearchResolveAttemptStatus.Success, identity);
    }
}
