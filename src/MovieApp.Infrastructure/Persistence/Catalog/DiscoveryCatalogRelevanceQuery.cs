using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Catalog;

internal static class DiscoveryCatalogRelevanceQuery
{
    internal static IQueryable<Movie> WhereDiscoveryRelevant(
        IQueryable<Movie> query,
        DiscoveryCatalogRelevanceThresholds thresholds,
        DateOnly today)
    {
        var newReleasesEarliest = today.AddDays(-thresholds.NewReleasesMaxAgeDays);
        var upcomingLatest = today.AddDays(thresholds.UpcomingReleaseHorizonDays);
        var seenCutoff = thresholds.ProviderDiscoverySeenCutoffUtc;

        return query.Where(movie =>
            movie.TmdbId.HasValue
            && (
                (movie.ReleaseDate.HasValue
                 && movie.ReleaseDate >= newReleasesEarliest
                 && movie.ReleaseDate <= today)
                || (movie.ReleaseDate.HasValue
                    && movie.ReleaseDate > today
                    && movie.ReleaseDate <= upcomingLatest)
                || movie.VoteCount >= thresholds.RecommendationCandidateMinVoteCount
                || movie.VoteCount >= thresholds.TopRatedMinVoteCountMovie
                || movie.VoteCount >= thresholds.PopularMinVoteCountMovie
                || movie.VoteCount >= thresholds.NewReleasesMinVoteCountMovie
                || (movie.TmdbProviderDiscoverySeenAtUtc != null
                    && movie.TmdbProviderDiscoverySeenAtUtc >= seenCutoff)));
    }

    internal static IQueryable<TvShow> WhereDiscoveryRelevant(
        IQueryable<TvShow> query,
        DiscoveryCatalogRelevanceThresholds thresholds,
        DateOnly today)
    {
        var newReleasesEarliest = today.AddDays(-thresholds.NewReleasesMaxAgeDays);
        var upcomingLatest = today.AddDays(thresholds.UpcomingReleaseHorizonDays);
        var seenCutoff = thresholds.ProviderDiscoverySeenCutoffUtc;

        return query.Where(tvShow =>
            tvShow.TmdbId.HasValue
            && (
                (tvShow.FirstAirDate.HasValue
                 && tvShow.FirstAirDate >= newReleasesEarliest
                 && tvShow.FirstAirDate <= today)
                || (tvShow.FirstAirDate.HasValue
                    && tvShow.FirstAirDate > today
                    && tvShow.FirstAirDate <= upcomingLatest)
                || tvShow.VoteCount >= thresholds.RecommendationCandidateMinVoteCount
                || tvShow.VoteCount >= thresholds.TopRatedMinVoteCountTv
                || tvShow.VoteCount >= thresholds.PopularMinVoteCountTv
                || tvShow.VoteCount >= thresholds.NewReleasesMinVoteCountTv
                || (tvShow.TmdbProviderDiscoverySeenAtUtc != null
                    && tvShow.TmdbProviderDiscoverySeenAtUtc >= seenCutoff)));
    }
}
