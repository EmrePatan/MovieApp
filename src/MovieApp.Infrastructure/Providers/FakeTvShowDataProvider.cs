using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeTvShowDataProvider(TvShowDataProviderCallTracker callTracker) : ITvShowDataProvider
{
    public const string BreakingBadExternalId = "fake-tv-900101";
    public const int BreakingBadTmdbId = 900101;
    public const int BreakingBadTvdbId = 900102;
    public const string BreakingBadImdbId = "tt9003747";
    public const string PagedCatalogQueryToken = "paged-catalog";
    public const int PagedCatalogTvShowCount = 25;

    private static readonly TvShowProviderDetails BreakingBadDetails = CreateBreakingBadDetails();

    private static readonly IReadOnlyList<TvShowProviderSummary> PagedCatalogSummaries =
        Enumerable.Range(1, PagedCatalogTvShowCount)
            .Select(index => new TvShowProviderSummary(
                ExternalId: $"fake-tv-{920000 + index}",
                TmdbId: 920000 + index,
                TvdbId: null,
                ImdbId: $"tt920000{index}",
                Title: $"Fake TV Show {index}",
                OriginalTitle: $"Fake TV Show {index}",
                Overview: $"Overview for fake TV show {index}.",
                FirstAirDate: new DateOnly(2018, 1, 1).AddDays(index),
                PosterPath: $"/fake/tv-{index}-poster.jpg",
                BackdropPath: $"/fake/tv-{index}-backdrop.jpg",
                OriginalLanguage: "en",
                VoteAverage: 7.5m,
                VoteCount: 500 + index))
            .ToList();

    public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        callTracker.RecordSearchTvShows();

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedQuery = QueryNormalizer.Normalize(query);

        if (normalizedQuery.Contains(PagedCatalogQueryToken, StringComparison.Ordinal))
        {
            return Task.FromResult(CreatePagedResult(PagedCatalogSummaries, page, pageSize));
        }

        if (!normalizedQuery.Contains("breaking", StringComparison.Ordinal) &&
            !normalizedQuery.Contains("breaking bad", StringComparison.Ordinal))
        {
            return Task.FromResult(CreatePagedResult([], page, pageSize));
        }

        var summary = ToSummary(BreakingBadDetails);
        return Task.FromResult(CreatePagedResult([summary], page, pageSize));
    }

    public Task<TvShowProviderDetails?> GetTvShowAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(externalId, BreakingBadExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<TvShowProviderDetails?>(BreakingBadDetails);
        }

        if (TryParsePagedCatalogExternalId(externalId, out var index))
        {
            return Task.FromResult<TvShowProviderDetails?>(CreatePagedCatalogDetails(index));
        }

        return Task.FromResult<TvShowProviderDetails?>(null);
    }

    public Task<SeasonProviderDetails?> GetSeasonAsync(
        string externalTvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(externalTvShowId, BreakingBadExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<SeasonProviderDetails?>(null);
        }

        return Task.FromResult(CreateBreakingBadSeason(seasonNumber));
    }

    public Task<EpisodeProviderDetails?> GetEpisodeAsync(
        string externalTvShowId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        var season = CreateBreakingBadSeason(seasonNumber);
        if (season is null)
        {
            return Task.FromResult<EpisodeProviderDetails?>(null);
        }

        var episode = season.Episodes.FirstOrDefault(item => item.EpisodeNumber == episodeNumber);
        return Task.FromResult(episode);
    }

    private static TvShowProviderDetails CreateBreakingBadDetails()
    {
        var seasons = new List<SeasonProviderSummary>
        {
            new(1, "Season 1", new DateOnly(2008, 1, 20), 3, "/fake/breaking-bad-s1-poster.jpg"),
            new(2, "Season 2", new DateOnly(2009, 3, 8), 2, "/fake/breaking-bad-s2-poster.jpg"),
            new(3, "Season 3", new DateOnly(2010, 3, 21), 2, "/fake/breaking-bad-s3-poster.jpg")
        };

        return new TvShowProviderDetails(
            ExternalId: BreakingBadExternalId,
            TmdbId: BreakingBadTmdbId,
            TvdbId: BreakingBadTvdbId,
            ImdbId: BreakingBadImdbId,
            Title: "Breaking Bad",
            OriginalTitle: "Breaking Bad",
            Overview: "A high school chemistry teacher turned methamphetamine producer partners with a former student.",
            FirstAirDate: new DateOnly(2008, 1, 20),
            LastAirDate: new DateOnly(2010, 6, 13),
            PosterPath: "/fake/breaking-bad-poster.jpg",
            BackdropPath: "/fake/breaking-bad-backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 9.5m,
            VoteCount: 12000,
            Status: "Ended",
            Genres: ["Crime", "Drama", "Thriller"],
            Seasons: seasons);
    }

    private static SeasonProviderDetails? CreateBreakingBadSeason(int seasonNumber)
    {
        return seasonNumber switch
        {
            1 => new SeasonProviderDetails(
                BreakingBadExternalId,
                TmdbId: 900201,
                TvdbId: 900202,
                SeasonNumber: 1,
                Name: "Season 1",
                Overview: "Walter White begins producing methamphetamine with Jesse Pinkman.",
                AirDate: new DateOnly(2008, 1, 20),
                EpisodeCount: 3,
                PosterPath: "/fake/breaking-bad-s1-poster.jpg",
                Episodes:
                [
                    CreateEpisode(1, 1, "Pilot", "Walter White is diagnosed with cancer.", new DateOnly(2008, 1, 20), 58, 8.2m, 2100),
                    CreateEpisode(1, 2, "Cat's in the Bag...", "Walter and Jesse deal with a body.", new DateOnly(2008, 1, 27), 48, 8.1m, 1900),
                    CreateEpisode(1, 3, "...And the Bag's in the River", "Walter faces a moral dilemma.", new DateOnly(2008, 2, 10), 48, 8.3m, 1850)
                ]),
            2 => new SeasonProviderDetails(
                BreakingBadExternalId,
                TmdbId: 900211,
                TvdbId: 900212,
                SeasonNumber: 2,
                Name: "Season 2",
                Overview: "The partnership expands and the stakes rise.",
                AirDate: new DateOnly(2009, 3, 8),
                EpisodeCount: 2,
                PosterPath: "/fake/breaking-bad-s2-poster.jpg",
                Episodes:
                [
                    CreateEpisode(2, 1, "Seven Thirty-Seven", "Tensions escalate with Tuco Salamanca.", new DateOnly(2009, 3, 8), 47, 8.4m, 1750),
                    CreateEpisode(2, 2, "Grilled", "Walter and Jesse are held captive.", new DateOnly(2009, 3, 15), 47, 8.6m, 1800)
                ]),
            3 => new SeasonProviderDetails(
                BreakingBadExternalId,
                TmdbId: 900221,
                TvdbId: 900222,
                SeasonNumber: 3,
                Name: "Season 3",
                Overview: "Walter and Jesse navigate a dangerous new landscape.",
                AirDate: new DateOnly(2010, 3, 21),
                EpisodeCount: 2,
                PosterPath: "/fake/breaking-bad-s3-poster.jpg",
                Episodes:
                [
                    CreateEpisode(3, 1, "No Más", "Walter adjusts to life after the plane crash.", new DateOnly(2010, 3, 21), 47, 8.5m, 1700),
                    CreateEpisode(3, 2, "Caballo sin Nombre", "Walter faces new threats.", new DateOnly(2010, 3, 28), 47, 8.4m, 1650)
                ]),
            _ => null
        };
    }

    private static EpisodeProviderDetails CreateEpisode(
        int seasonNumber,
        int episodeNumber,
        string name,
        string overview,
        DateOnly airDate,
        int runtimeMinutes,
        decimal voteAverage,
        int voteCount) =>
        new(
            BreakingBadExternalId,
            TmdbId: 900300 + seasonNumber * 10 + episodeNumber,
            TvdbId: null,
            ImdbId: $"tt9003{seasonNumber:D2}{episodeNumber:D2}",
            SeasonNumber: seasonNumber,
            EpisodeNumber: episodeNumber,
            Name: name,
            Overview: overview,
            AirDate: airDate,
            RuntimeMinutes: runtimeMinutes,
            StillPath: $"/fake/breaking-bad-s{seasonNumber}e{episodeNumber}.jpg",
            VoteAverage: voteAverage,
            VoteCount: voteCount);

    private static TvShowProviderSummary ToSummary(TvShowProviderDetails details) =>
        new(
            details.ExternalId,
            details.TmdbId,
            details.TvdbId,
            details.ImdbId,
            details.Title,
            details.OriginalTitle,
            details.Overview,
            details.FirstAirDate,
            details.PosterPath,
            details.BackdropPath,
            details.OriginalLanguage,
            details.VoteAverage,
            details.VoteCount);

    private static TvShowProviderSearchResult CreatePagedResult(
        IReadOnlyList<TvShowProviderSummary> allResults,
        int page,
        int pageSize)
    {
        var totalCount = allResults.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (page - 1) * pageSize;
        var pageResults = allResults.Skip(skip).Take(pageSize).ToList();

        return new TvShowProviderSearchResult(
            pageResults,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static bool TryParsePagedCatalogExternalId(string externalId, out int index)
    {
        const string prefix = "fake-tv-";

        if (!externalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            index = 0;
            return false;
        }

        if (!int.TryParse(externalId[prefix.Length..], out var tmdbId))
        {
            index = 0;
            return false;
        }

        index = tmdbId - 920000;
        return index >= 1 && index <= PagedCatalogTvShowCount;
    }

    private static TvShowProviderDetails CreatePagedCatalogDetails(int index) =>
        new(
            ExternalId: $"fake-tv-{920000 + index}",
            TmdbId: 920000 + index,
            TvdbId: null,
            ImdbId: $"tt920000{index}",
            Title: $"Fake TV Show {index}",
            OriginalTitle: $"Fake TV Show {index}",
            Overview: $"Overview for fake TV show {index}.",
            FirstAirDate: new DateOnly(2018, 1, 1).AddDays(index),
            LastAirDate: null,
            PosterPath: $"/fake/tv-{index}-poster.jpg",
            BackdropPath: $"/fake/tv-{index}-backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 7.5m,
            VoteCount: 500 + index,
            Status: "Ended",
            Genres: ["Drama"],
            Seasons: []);
}
