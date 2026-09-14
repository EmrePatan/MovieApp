using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.TvShows;

public sealed class TvShowSeasonSummaryHydratorTests
{
    private static readonly Guid TvShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task EnsureSeasonSummariesAsyncHydratesWhenRegularSeasonsAreMissing()
    {
        var repository = new FakeTvShowRepository(CreateTvShowWithoutSeasons());
        var provider = new FakeTvShowDataProvider();
        var hydrator = new TvShowSeasonSummaryHydrator(repository, provider, new FakeExternalIdResolver());

        var hydrationResult = await hydrator.EnsureSeasonSummariesAsync(TvShowId);

        Assert.Equal(3, hydrationResult.TvShow.Seasons.Count);
        Assert.True(hydrationResult.ProviderCatalogRefreshed);
        Assert.Equal(1, provider.GetTvShowCalls);
        Assert.Equal(1, repository.UpsertCalls);
    }

    [Fact]
    public async Task EnsureSeasonSummariesAsyncSkipsProviderWhenRegularSeasonsAlreadyExist()
    {
        var tvShow = CreateTvShowWithoutSeasons();
        tvShow.Seasons.Add(new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = TvShowId,
            SeasonNumber = 1,
            EpisodeCount = 24,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        var repository = new FakeTvShowRepository(tvShow);
        var provider = new FakeTvShowDataProvider();
        var hydrator = new TvShowSeasonSummaryHydrator(repository, provider, new FakeExternalIdResolver());

        var hydrationResult = await hydrator.EnsureSeasonSummariesAsync(TvShowId);

        Assert.Single(hydrationResult.TvShow.Seasons);
        Assert.False(hydrationResult.ProviderCatalogRefreshed);
        Assert.Equal(0, provider.GetTvShowCalls);
        Assert.Equal(0, repository.UpsertCalls);
    }

    [Fact]
    public async Task EnsureSeasonSummariesAsyncThrowsWhenTvShowMissing()
    {
        var hydrator = new TvShowSeasonSummaryHydrator(
            new FakeTvShowRepository(null),
            new FakeTvShowDataProvider(),
            new FakeExternalIdResolver());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            hydrator.EnsureSeasonSummariesAsync(TvShowId));
    }

    private static TvShow CreateTvShowWithoutSeasons() =>
        new()
        {
            Id = TvShowId,
            TmdbId = 900101,
            Title = "Breaking Bad",
            Status = TvShowStatus.Ended,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private sealed class FakeExternalIdResolver : ITvShowExternalIdResolver
    {
        public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId) => "fake-tv-900101";
    }

    private sealed class FakeTvShowDataProvider : ITvShowDataProvider
    {
        public int GetTvShowCalls { get; private set; }

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            CancellationToken cancellationToken = default)
        {
            GetTvShowCalls++;
            return Task.FromResult<TvShowProviderDetails?>(new TvShowProviderDetails(
                externalId,
                900101,
                null,
                null,
                "Breaking Bad",
                null,
                "Overview",
                new DateOnly(2008, 1, 20),
                null,
                "/poster.jpg",
                null,
                "en",
                9.5m,
                1000,
                "Ended",
                ["Drama"],
                [
                    new SeasonProviderSummary(1, "Season 1", new DateOnly(2008, 1, 20), 3, null),
                    new SeasonProviderSummary(2, "Season 2", new DateOnly(2009, 3, 8), 2, null),
                    new SeasonProviderSummary(3, "Season 3", new DateOnly(2010, 3, 21), 2, null),
                ]));
        }

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowRepository(TvShow? tvShow) : ITvShowRepository
    {
        public int UpsertCalls { get; private set; }

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(tvShow);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            var hydrated = CreateTvShowWithoutSeasons();
            foreach (var season in details.Seasons)
            {
                hydrated.Seasons.Add(new Season
                {
                    Id = Guid.NewGuid(),
                    TvShowId = hydrated.Id,
                    SeasonNumber = season.SeasonNumber,
                    Name = season.Name,
                    AirDate = season.AirDate,
                    EpisodeCount = season.EpisodeCount,
                    PosterPath = season.PosterPath,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }

            return Task.FromResult(hydrated);
        }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
