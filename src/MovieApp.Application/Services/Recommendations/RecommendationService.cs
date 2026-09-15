using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Recommendations;

public sealed class RecommendationService(
    IRecommendationRepository recommendationRepository,
    IDiscoveryService discoveryService,
    ICurrentUser currentUser,
    ICacheService cacheService,
    IOptions<RecommendationOptions> options) : IRecommendationService
{
    private static readonly TimeSpan SimilarCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PersonalizedCacheTtl = TimeSpan.FromMinutes(5);

    private readonly RecommendationOptions _options = options.Value;

    public async Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(
        Guid movieId,
        SimilarContentCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateSimilarCriteria(criteria);

        var cacheKey = RecommendationCacheKeys.SimilarMovie(movieId, criteria.Page, criteria.PageSize);
        var cached = await cacheService.GetAsync<RecommendationCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var source = await recommendationRepository.GetMovieSimilarityProfileAsync(movieId, cancellationToken);
        if (source is null)
        {
            throw new NotFoundException("Movie not found.");
        }

        var candidates = await recommendationRepository.GetSimilarMovieCandidatesAsync(
            movieId,
            source.GenreIds,
            _options.MaximumCandidates,
            cancellationToken);

        var ranked = SimilarityEngine.RankSimilarCandidates(source, candidates, _options)
            .Select(item => RecommendationMapper.ToRecommendationItem(
                item.Candidate,
                item.Score,
                RecommendationReasonBuilder.BuildSimilarReason(source, item.Candidate)))
            .ToList();

        var result = Paginate(ranked, criteria.Page, criteria.PageSize);

        await cacheService.SetAsync(
            cacheKey,
            new RecommendationCacheEntry { Result = result },
            SimilarCacheTtl,
            cancellationToken);

        return result;
    }

    public async Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(
        Guid tvShowId,
        SimilarContentCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateSimilarCriteria(criteria);

        var cacheKey = RecommendationCacheKeys.SimilarTv(tvShowId, criteria.Page, criteria.PageSize);
        var cached = await cacheService.GetAsync<RecommendationCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var source = await recommendationRepository.GetTvShowSimilarityProfileAsync(tvShowId, cancellationToken);
        if (source is null)
        {
            throw new NotFoundException("TV show not found.");
        }

        var candidates = await recommendationRepository.GetSimilarTvShowCandidatesAsync(
            tvShowId,
            source.GenreIds,
            _options.MaximumCandidates,
            cancellationToken);

        var ranked = SimilarityEngine.RankSimilarCandidates(source, candidates, _options)
            .Select(item => RecommendationMapper.ToRecommendationItem(
                item.Candidate,
                item.Score,
                RecommendationReasonBuilder.BuildSimilarReason(source, item.Candidate)))
            .ToList();

        var result = Paginate(ranked, criteria.Page, criteria.PageSize);

        await cacheService.SetAsync(
            cacheKey,
            new RecommendationCacheEntry { Result = result },
            SimilarCacheTtl,
            cancellationToken);

        return result;
    }

    public async Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(
        RecommendationCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateRecommendationCriteria(criteria);
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var cacheKey = RecommendationCacheKeys.User(userId, criteria.Type, criteria.Page, criteria.PageSize);
        var cached = await cacheService.GetAsync<RecommendationCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var context = await recommendationRepository.GetUserRecommendationContextAsync(userId, cancellationToken);
        PaginatedResult<RecommendationItem> result;

        if (context.MeaningfulInteractionCount < _options.MinimumPersonalizationInteractions)
        {
            result = await BuildColdStartRecommendationsAsync(criteria, cancellationToken);
        }
        else
        {
            result = await BuildPersonalizedRecommendationsAsync(context, criteria, cancellationToken);
        }

        await cacheService.SetAsync(
            cacheKey,
            new RecommendationCacheEntry { Result = result },
            PersonalizedCacheTtl,
            cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
        bool includeColdStartDiscoverySections = true,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var cacheKey = RecommendationCacheKeys.Home(userId);
        var cached = await cacheService.GetAsync<RecommendationHomeCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Sections;
        }

        var context = await recommendationRepository.GetUserRecommendationContextAsync(userId, cancellationToken);
        IReadOnlyList<RecommendationSection> sections;

        if (context.MeaningfulInteractionCount < _options.MinimumPersonalizationInteractions)
        {
            sections = includeColdStartDiscoverySections
                ? await BuildColdStartHomeSectionsAsync(cancellationToken)
                : [];
        }
        else
        {
            sections = await BuildPersonalizedHomeSectionsAsync(context, cancellationToken);
        }

        await cacheService.SetAsync(
            cacheKey,
            new RecommendationHomeCacheEntry { Sections = sections },
            PersonalizedCacheTtl,
            cancellationToken);

        return sections;
    }

    private async Task<PaginatedResult<RecommendationItem>> BuildColdStartRecommendationsAsync(
        RecommendationCriteria criteria,
        CancellationToken cancellationToken)
    {
        var discoveryType = MapToSearchContentType(criteria.Type);
        var discovery = await discoveryService.GetPopularAsync(
            new DiscoveryCriteria(discoveryType, criteria.Page, criteria.PageSize),
            cancellationToken);

        var items = discovery.Items
            .Select(item => RecommendationMapper.ToColdStartItem(item, RecommendationReasonBuilder.BuildColdStartPopularReason()))
            .ToList();

        return new PaginatedResult<RecommendationItem>(
            items,
            discovery.Page,
            discovery.PageSize,
            discovery.TotalCount,
            discovery.TotalPages);
    }

    private async Task<PaginatedResult<RecommendationItem>> BuildPersonalizedRecommendationsAsync(
        UserRecommendationContext context,
        RecommendationCriteria criteria,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var genrePreferences = PersonalizedRecommendationEngine.BuildGenrePreferences(
            context.Signals,
            _options,
            utcNow);
        var keywordPreferences = KeywordAffinityScorer.BuildKeywordPreferences(
            context.Signals,
            _options,
            utcNow);
        var preferredGenreIds = genrePreferences.Keys.ToList();

        var candidates = await recommendationRepository.GetPersonalizedCandidatesAsync(
            criteria.Type,
            preferredGenreIds,
            context.ExcludedMovieIds,
            context.ExcludedTvShowIds,
            _options.MaximumCandidates,
            cancellationToken);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            candidates,
            context.Signals,
            genrePreferences,
            keywordPreferences,
            _options,
            utcNow);

        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(scored, _options)
            .Select(RecommendationMapper.ToRecommendationItem)
            .ToList();

        return Paginate(diversified, criteria.Page, criteria.PageSize);
    }

    private async Task<IReadOnlyList<RecommendationSection>> BuildColdStartHomeSectionsAsync(
        CancellationToken cancellationToken)
    {
        var sectionSize = _options.HomeSectionItemCount;
        var popular = await discoveryService.GetPopularAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, sectionSize),
            cancellationToken);
        var trending = await discoveryService.GetTrendingAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, sectionSize),
            cancellationToken);
        var topRated = await discoveryService.GetTopRatedAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, sectionSize),
            cancellationToken);

        return
        [
            CreateSection(
                "popular",
                "Popular",
                popular.Items.Select(item => RecommendationMapper.ToColdStartItem(
                    item,
                    RecommendationReasonBuilder.BuildColdStartPopularReason()))),
            CreateSection(
                "trending",
                "Trending",
                trending.Items.Select(item => RecommendationMapper.ToColdStartItem(
                    item,
                    RecommendationReasonBuilder.BuildColdStartTrendingReason()))),
            CreateSection(
                "top-rated",
                "Top Rated",
                topRated.Items
                    .OrderByDescending(item => item.VoteAverage)
                    .ThenByDescending(item => item.VoteCount)
                    .Select(item => RecommendationMapper.ToColdStartItem(
                        item,
                        RecommendationReasonBuilder.BuildColdStartTopRatedReason())))
        ];
    }

    private async Task<IReadOnlyList<RecommendationSection>> BuildPersonalizedHomeSectionsAsync(
        UserRecommendationContext context,
        CancellationToken cancellationToken)
    {
        var sections = new List<RecommendationSection>();
        var sectionSize = _options.HomeSectionItemCount;

        var recommended = await BuildPersonalizedRecommendationsAsync(
            context,
            new RecommendationCriteria(RecommendationContentType.All, 1, sectionSize),
            cancellationToken);

        sections.Add(CreateSection(
            "recommended-for-you",
            "Recommended For You",
            recommended.Items));

        var becauseYouWatched = await BuildBecauseYouWatchedSectionAsync(context, sectionSize, cancellationToken);
        if (becauseYouWatched.Count > 0)
        {
            sections.Add(CreateSection("because-you-watched", "Because You Watched", becauseYouWatched));
        }

        return sections;
    }

    private async Task<IReadOnlyList<RecommendationItem>> BuildBecauseYouWatchedSectionAsync(
        UserRecommendationContext context,
        int sectionSize,
        CancellationToken cancellationToken)
    {
        var watchedSources = context.Signals
            .Where(signal => signal.SignalType == UserBehaviorSignalTypes.Watched)
            .Take(3)
            .ToList();

        if (watchedSources.Count == 0)
        {
            return [];
        }

        return await BuildSimilarSectionFromSignalsAsync(
            watchedSources,
            context,
            sectionSize,
            cancellationToken);
    }

    private async Task<IReadOnlyList<RecommendationItem>> BuildSimilarSectionFromSignalsAsync(
        IReadOnlyList<UserBehaviorSignal> sourceSignals,
        UserRecommendationContext context,
        int sectionSize,
        CancellationToken cancellationToken)
    {
        var aggregated = new Dictionary<(Guid Id, string Type), RecommendationItem>();
        var movieSignals = sourceSignals.Where(signal => signal.ContentType == "movie").ToList();
        var tvSignals = sourceSignals.Where(signal => signal.ContentType == "tv").ToList();

        await AggregateSimilarMovieSignalsAsync(
            movieSignals,
            context.ExcludedMovieIds,
            aggregated,
            cancellationToken);
        await AggregateSimilarTvSignalsAsync(
            tvSignals,
            context.ExcludedTvShowIds,
            aggregated,
            cancellationToken);

        return aggregated.Values
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.VoteCount)
            .ThenByDescending(item => item.VoteAverage)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(sectionSize)
            .ToList();
    }

    private async Task AggregateSimilarMovieSignalsAsync(
        List<UserBehaviorSignal> movieSignals,
        IReadOnlySet<Guid> excludedMovieIds,
        Dictionary<(Guid Id, string Type), RecommendationItem> aggregated,
        CancellationToken cancellationToken)
    {
        if (movieSignals.Count == 0)
        {
            return;
        }

        var movieIds = movieSignals.Select(signal => signal.ContentId).Distinct().ToList();
        var sourceProfiles = await recommendationRepository.GetMovieSimilarityProfilesAsync(movieIds, cancellationToken);
        var sourceRequests = movieSignals
            .Where(signal => sourceProfiles.ContainsKey(signal.ContentId))
            .Select(signal => new SimilaritySourceGenreRequest(
                signal.ContentId,
                sourceProfiles[signal.ContentId].GenreIds))
            .DistinctBy(request => request.SourceId)
            .ToList();
        var candidateIdsBySource = await recommendationRepository.GetSimilarMovieCandidateIdsForSourcesAsync(
            sourceRequests,
            _options.MaximumCandidates,
            cancellationToken);

        var uniqueCandidateIds = candidateIdsBySource.Values
            .SelectMany(ids => ids)
            .Distinct()
            .ToList();
        var candidateProfiles = await recommendationRepository.GetSimilarMovieCandidatesByIdsAsync(
            uniqueCandidateIds,
            cancellationToken);
        var candidatesById = candidateProfiles.ToDictionary(candidate => candidate.Id);

        foreach (var signal in movieSignals)
        {
            if (!sourceProfiles.TryGetValue(signal.ContentId, out var source) ||
                !candidateIdsBySource.TryGetValue(signal.ContentId, out var candidateIds) ||
                candidateIds.Count == 0)
            {
                continue;
            }

            var candidates = candidateIds
                .Where(candidatesById.ContainsKey)
                .Select(id => candidatesById[id])
                .ToList();

            foreach (var (candidate, score) in SimilarityEngine.RankSimilarCandidates(source, candidates, _options))
            {
                if (excludedMovieIds.Contains(candidate.Id) || candidate.Id == signal.ContentId)
                {
                    continue;
                }

                AddAggregatedItem(
                    aggregated,
                    RecommendationMapper.ToRecommendationItem(
                        candidate,
                        score,
                        RecommendationReasonBuilder.BuildSimilarReason(source, candidate)));
            }
        }
    }

    private async Task AggregateSimilarTvSignalsAsync(
        List<UserBehaviorSignal> tvSignals,
        IReadOnlySet<Guid> excludedTvShowIds,
        Dictionary<(Guid Id, string Type), RecommendationItem> aggregated,
        CancellationToken cancellationToken)
    {
        if (tvSignals.Count == 0)
        {
            return;
        }

        var tvShowIds = tvSignals.Select(signal => signal.ContentId).Distinct().ToList();
        var sourceProfiles = await recommendationRepository.GetTvShowSimilarityProfilesAsync(tvShowIds, cancellationToken);
        var sourceRequests = tvSignals
            .Where(signal => sourceProfiles.ContainsKey(signal.ContentId))
            .Select(signal => new SimilaritySourceGenreRequest(
                signal.ContentId,
                sourceProfiles[signal.ContentId].GenreIds))
            .DistinctBy(request => request.SourceId)
            .ToList();
        var candidateIdsBySource = await recommendationRepository.GetSimilarTvShowCandidateIdsForSourcesAsync(
            sourceRequests,
            _options.MaximumCandidates,
            cancellationToken);

        var uniqueCandidateIds = candidateIdsBySource.Values
            .SelectMany(ids => ids)
            .Distinct()
            .ToList();
        var candidateProfiles = await recommendationRepository.GetSimilarTvShowCandidatesByIdsAsync(
            uniqueCandidateIds,
            cancellationToken);
        var candidatesById = candidateProfiles.ToDictionary(candidate => candidate.Id);

        foreach (var signal in tvSignals)
        {
            if (!sourceProfiles.TryGetValue(signal.ContentId, out var source) ||
                !candidateIdsBySource.TryGetValue(signal.ContentId, out var candidateIds) ||
                candidateIds.Count == 0)
            {
                continue;
            }

            var candidates = candidateIds
                .Where(candidatesById.ContainsKey)
                .Select(id => candidatesById[id])
                .ToList();

            foreach (var (candidate, score) in SimilarityEngine.RankSimilarCandidates(source, candidates, _options))
            {
                if (excludedTvShowIds.Contains(candidate.Id) || candidate.Id == signal.ContentId)
                {
                    continue;
                }

                AddAggregatedItem(
                    aggregated,
                    RecommendationMapper.ToRecommendationItem(
                        candidate,
                        score,
                        RecommendationReasonBuilder.BuildSimilarReason(source, candidate)));
            }
        }
    }

    private static void AddAggregatedItem(
        Dictionary<(Guid Id, string Type), RecommendationItem> aggregated,
        RecommendationItem item)
    {
        var key = (item.Id, item.Type);
        if (!aggregated.TryGetValue(key, out var existing) || item.Score > existing.Score)
        {
            aggregated[key] = item;
        }
    }

    private static RecommendationSection CreateSection(
        string key,
        string title,
        IEnumerable<RecommendationItem> items) =>
        new(key, title, items.ToList());

    private static PaginatedResult<RecommendationItem> Paginate(
        List<RecommendationItem> items,
        int page,
        int pageSize)
    {
        var totalCount = items.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var pageItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResult<RecommendationItem>(pageItems, page, pageSize, totalCount, totalPages);
    }

    private static SearchContentType MapToSearchContentType(RecommendationContentType type) =>
        type switch
        {
            RecommendationContentType.Movie => SearchContentType.Movie,
            RecommendationContentType.Tv => SearchContentType.Tv,
            _ => SearchContentType.All
        };

    private static void ValidateSimilarCriteria(SimilarContentCriteria criteria)
    {
        var validation = RecommendationValidator.ValidatePagination(criteria.Page, criteria.PageSize);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }
    }

    private static void ValidateRecommendationCriteria(RecommendationCriteria criteria)
    {
        var paginationValidation = RecommendationValidator.ValidatePagination(criteria.Page, criteria.PageSize);
        if (!paginationValidation.IsValid)
        {
            throw new ValidationException(paginationValidation.ErrorMessage!);
        }
    }
}
