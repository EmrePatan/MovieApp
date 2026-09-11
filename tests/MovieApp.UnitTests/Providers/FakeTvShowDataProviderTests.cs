using MovieApp.Application.Models.Common;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.UnitTests.Providers;

public sealed class FakeTvShowDataProviderTests
{
    [Fact]
    public async Task SearchTvShowsAsyncReturnsBreakingBadForNormalizedQuery()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var result = await provider.SearchTvShowsAsync(
            " BREAKING BAD ",
            SearchPaginationDefaults.DefaultPage,
            SearchPaginationDefaults.DefaultPageSize);

        Assert.Single(result.Results);
        Assert.Equal("Breaking Bad", result.Results[0].Title);
        Assert.Equal(FakeTvShowDataProvider.BreakingBadTmdbId, result.Results[0].TmdbId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchTvShowsAsyncReturnsEmptyPageBeyondAvailableResults()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var result = await provider.SearchTvShowsAsync("breaking bad", 2, 20);

        Assert.Empty(result.Results);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task SearchTvShowsAsyncReturnsPagedCatalogWithMetadata()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var result = await provider.SearchTvShowsAsync(
            FakeTvShowDataProvider.PagedCatalogQueryToken,
            1,
            10);

        Assert.Equal(10, result.Results.Count);
        Assert.Equal(FakeTvShowDataProvider.PagedCatalogTvShowCount, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetTvShowAsyncReturnsBreakingBadDetailsWithSeasons()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var details = await provider.GetTvShowAsync(FakeTvShowDataProvider.BreakingBadExternalId);

        Assert.NotNull(details);
        Assert.Equal("Ended", details.Status);
        Assert.Equal(3, details.Genres.Count);
        Assert.Equal(3, details.Seasons.Count);
    }

    [Fact]
    public async Task GetSeasonAsyncReturnsSeasonWithEpisodes()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var season = await provider.GetSeasonAsync(FakeTvShowDataProvider.BreakingBadExternalId, 1);

        Assert.NotNull(season);
        Assert.Equal(1, season.SeasonNumber);
        Assert.Equal(3, season.Episodes.Count);
        Assert.Equal("Pilot", season.Episodes[0].Name);
    }

    [Fact]
    public async Task GetSeasonAsyncReturnsNullForMissingSeason()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var season = await provider.GetSeasonAsync(FakeTvShowDataProvider.BreakingBadExternalId, 99);

        Assert.Null(season);
    }

    [Fact]
    public async Task GetEpisodeAsyncReturnsEpisodeDetails()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var episode = await provider.GetEpisodeAsync(
            FakeTvShowDataProvider.BreakingBadExternalId,
            1,
            1);

        Assert.NotNull(episode);
        Assert.Equal("Pilot", episode.Name);
        Assert.Equal(58, episode.RuntimeMinutes);
    }

    [Fact]
    public async Task GetEpisodeAsyncReturnsNullForMissingEpisode()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var episode = await provider.GetEpisodeAsync(
            FakeTvShowDataProvider.BreakingBadExternalId,
            1,
            99);

        Assert.Null(episode);
    }
}
