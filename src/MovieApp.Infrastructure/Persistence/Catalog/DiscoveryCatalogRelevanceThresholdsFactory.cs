using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Search;

namespace MovieApp.Infrastructure.Persistence.Catalog;

internal static class DiscoveryCatalogRelevanceThresholdsFactory
{
    internal static DiscoveryCatalogRelevanceThresholds Create(
        DateTime utcNow,
        NewReleasesOptions newReleases,
        RecommendationOptions recommendations,
        TopRatedOptions topRated,
        CatalogMetadataFreshnessOptions freshness)
    {
        var seenCutoff = utcNow.AddDays(-freshness.ProviderDiscoverySeenWindowDays);

        return new DiscoveryCatalogRelevanceThresholds(
            newReleases.MaxAgeDays,
            newReleases.MinVoteCountMovie,
            newReleases.MinVoteCountTv,
            freshness.UpcomingReleaseHorizonDays,
            recommendations.CandidateMinVoteCount,
            topRated.MinVoteCountMovie,
            topRated.MinVoteCountTv,
            PopularDiscoverQuality.MinimumVoteCountMovie,
            PopularDiscoverQuality.MinimumVoteCountTv,
            seenCutoff);
    }
}
