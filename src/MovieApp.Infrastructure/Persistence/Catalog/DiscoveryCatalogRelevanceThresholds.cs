namespace MovieApp.Infrastructure.Persistence.Catalog;

internal sealed record DiscoveryCatalogRelevanceThresholds(
    int NewReleasesMaxAgeDays,
    int NewReleasesMinVoteCountMovie,
    int NewReleasesMinVoteCountTv,
    int UpcomingReleaseHorizonDays,
    int RecommendationCandidateMinVoteCount,
    int TopRatedMinVoteCountMovie,
    int TopRatedMinVoteCountTv,
    int PopularMinVoteCountMovie,
    int PopularMinVoteCountTv,
    DateTime ProviderDiscoverySeenCutoffUtc);
