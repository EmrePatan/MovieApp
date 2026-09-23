using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Discovery;

public sealed class PickSomethingService(
    IRecommendationRepository recommendationRepository,
    IDiscoveryService discoveryService,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService,
    ICurrentUser currentUser,
    IOptions<RecommendationOptions> options) : IPickSomethingService
{
    private const decimal WatchlistScoreBoost = 0.25m;
    private const string WatchlistReason = "From your watchlist";
    private const string ColdStartTrendingReason = "Trending right now";
    private const string ColdStartPopularReason = "Popular right now";

    private readonly RecommendationOptions _options = options.Value;

    public async Task<RecommendationItem?> PickAsync(
        PickSomethingCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var validation = PickSomethingValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        if (currentUser.IsAuthenticated && currentUser.UserId is Guid userId)
        {
            var context = await recommendationRepository.GetUserRecommendationContextAsync(
                userId,
                _options.MinimumPersonalizationInteractions,
                cancellationToken);

            if (context.MeaningfulInteractionCount >= _options.MinimumPersonalizationInteractions)
            {
                return await BuildPersonalizedPickAsync(context, criteria, contentLocale, cancellationToken);
            }
        }

        return await BuildColdStartPickAsync(criteria, contentLocale, cancellationToken);
    }

    private async Task<RecommendationItem?> BuildColdStartPickAsync(
        PickSomethingCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var discoveryType = MapToSearchContentType(criteria.MediaType);
        var trending = await discoveryService.GetTrendingAsync(
            new DiscoveryCriteria(discoveryType, 1, PickSomethingSelector.CandidatePoolSize),
            contentLocale,
            cancellationToken);

        var trendingItems = FilterColdStartItems(trending.Items, criteria);
        if (trendingItems.Count > 0)
        {
            var trendingPick = PickSomethingSelector.SelectFromBand(
                RankColdStartItems(trendingItems, ColdStartTrendingReason));
            return trendingPick is null
                ? null
                : await LocalizePickAsync(trendingPick, contentLocale, cancellationToken);
        }

        var popular = await discoveryService.GetPopularAsync(
            new DiscoveryCriteria(discoveryType, 1, PickSomethingSelector.CandidatePoolSize),
            contentLocale,
            cancellationToken);
        var popularItems = FilterColdStartItems(popular.Items, criteria);
        if (popularItems.Count == 0)
        {
            return null;
        }

        var pick = PickSomethingSelector.SelectFromBand(RankColdStartItems(popularItems, ColdStartPopularReason));
        return pick is null ? null : await LocalizePickAsync(pick, contentLocale, cancellationToken);
    }

    private async Task<RecommendationItem> LocalizePickAsync(
        RecommendationItem pick,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return pick;
        }

        var localized = await summaryLocalizationOverlayService.ApplyToRecommendationItemsAsync(
            new PaginatedResult<RecommendationItem>(
                [pick],
                1,
                1,
                1,
                1),
            contentLocale,
            cancellationToken);

        var localizedPick = localized.Items[0];
        return localizedPick with
        {
            Reason = RecommendationReasonLocalization.Localize(localizedPick.Reason, contentLocale),
        };
    }

    private async Task<RecommendationItem?> BuildPersonalizedPickAsync(
        UserRecommendationContext context,
        PickSomethingCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var watchlistMovieIds = GetWatchlistIds(context, "movie");
        var watchlistTvShowIds = GetWatchlistIds(context, "tv");
        var excludedMovieIds = BuildMovieExclusions(context, watchlistMovieIds, criteria.SessionExcludedIds);
        var excludedTvShowIds = BuildTvShowExclusions(context, watchlistTvShowIds, criteria.SessionExcludedIds);

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
            criteria.MediaType,
            preferredGenreIds,
            excludedMovieIds,
            excludedTvShowIds,
            _options.MaximumCandidates,
            cancellationToken);

        if (candidates.Count == 0)
        {
            return await BuildColdStartPickAsync(criteria, contentLocale, cancellationToken);
        }

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            candidates,
            context.Signals,
            genrePreferences,
            keywordPreferences,
            _options,
            utcNow);

        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(scored, _options);
        if (!HasConfidentPersonalization(diversified))
        {
            return await BuildColdStartPickAsync(criteria, contentLocale, cancellationToken);
        }

        var ranked = ApplyWatchlistPreference(diversified, watchlistMovieIds, watchlistTvShowIds)
            .Select(RecommendationMapper.ToRecommendationItem)
            .Select(NormalizePickReason)
            .Take(PickSomethingSelector.CandidatePoolSize)
            .ToList();

        var pick = PickSomethingSelector.SelectFromBand(ranked);
        if (pick is null)
        {
            return null;
        }

        return await LocalizePickAsync(pick, contentLocale, cancellationToken);
    }

    private static bool HasConfidentPersonalization(IReadOnlyList<ScoredRecommendation> recommendations)
    {
        return recommendations
            .Take(PickSomethingSelector.SelectionBandSize)
            .Any(recommendation =>
                !string.IsNullOrWhiteSpace(recommendation.Reason) &&
                recommendation.Reason is not "Popular in your favorite genres");
    }

    private static RecommendationItem NormalizePickReason(RecommendationItem item) =>
        item.Reason switch
        {
            "Popular in your favorite genres" => item with { Reason = ColdStartPopularReason },
            _ => item
        };

    private static List<ScoredRecommendation> ApplyWatchlistPreference(
        IReadOnlyList<ScoredRecommendation> recommendations,
        HashSet<Guid> watchlistMovieIds,
        HashSet<Guid> watchlistTvShowIds)
    {
        return recommendations
            .Select(recommendation =>
            {
                var isWatchlistItem = recommendation.Candidate.Type == "movie"
                    ? watchlistMovieIds.Contains(recommendation.Candidate.Id)
                    : watchlistTvShowIds.Contains(recommendation.Candidate.Id);

                if (!isWatchlistItem)
                {
                    return recommendation;
                }

                return recommendation with
                {
                    Score = recommendation.Score + WatchlistScoreBoost,
                    Reason = WatchlistReason
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Candidate.VoteCount)
            .ThenByDescending(item => item.Candidate.VoteAverage)
            .ThenBy(item => item.Candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<Guid> GetWatchlistIds(UserRecommendationContext context, string contentType) =>
        context.Signals
            .Where(signal =>
                signal.SignalType == UserBehaviorSignalTypes.Watchlist &&
                signal.ContentType == contentType)
            .Select(signal => signal.ContentId)
            .ToHashSet();

    private static HashSet<Guid> BuildMovieExclusions(
        UserRecommendationContext context,
        HashSet<Guid> watchlistMovieIds,
        IReadOnlySet<Guid> sessionExcludedIds)
    {
        var exclusions = context.ExcludedMovieIds
            .Where(id => !watchlistMovieIds.Contains(id))
            .ToHashSet();
        exclusions.UnionWith(sessionExcludedIds);
        return exclusions;
    }

    private static HashSet<Guid> BuildTvShowExclusions(
        UserRecommendationContext context,
        HashSet<Guid> watchlistTvShowIds,
        IReadOnlySet<Guid> sessionExcludedIds)
    {
        var exclusions = context.ExcludedTvShowIds
            .Where(id => !watchlistTvShowIds.Contains(id))
            .ToHashSet();
        exclusions.UnionWith(sessionExcludedIds);
        return exclusions;
    }

    private static List<RecommendationItem> RankColdStartItems(
        IReadOnlyList<SearchItem> items,
        string reason) =>
        items
            .Select(item => RecommendationMapper.ToColdStartItem(item, reason))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.VoteCount)
            .ThenByDescending(item => item.VoteAverage)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<SearchItem> FilterColdStartItems(
        IReadOnlyList<SearchItem> items,
        PickSomethingCriteria criteria) =>
        items
            .Where(item => !criteria.SessionExcludedIds.Contains(item.Id))
            .Where(item => MatchesMediaType(item.Type, criteria.MediaType))
            .ToList();

    private static bool MatchesMediaType(string itemType, RecommendationContentType mediaType) =>
        mediaType switch
        {
            RecommendationContentType.Movie => itemType == "movie",
            RecommendationContentType.Tv => itemType == "tv",
            _ => itemType is "movie" or "tv"
        };

    private static SearchContentType MapToSearchContentType(RecommendationContentType type) =>
        type switch
        {
            RecommendationContentType.Movie => SearchContentType.Movie,
            RecommendationContentType.Tv => SearchContentType.Tv,
            _ => SearchContentType.All
        };
}
