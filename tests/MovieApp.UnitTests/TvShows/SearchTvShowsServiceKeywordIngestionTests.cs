using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.UnitTests.Keywords;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.TvShows;

public sealed class SearchTvShowsServiceKeywordIngestionTests
{
    [Fact]
    public async Task SearchAsyncRequestsKeywordsInBoundedDetailFetch()
    {
        var provider = new KeywordAwareTvShowDataProvider();
        var service = new SearchTvShowsService(
            provider,
            CatalogProviderUpsertTestDoubles.CreateRepositoryBackedUpsertService(
                tvShowRepository: new NoOpTvShowRepository()),
            new NoOpCatalogSyncStateService(),
            new SearchServiceTestsHelper.FakeCacheService(null),
            Options.Create(new SearchOptions
            {
                MaxProviderDetailFetchesPerContentType = 20,
                MaxConcurrentProviderHttpRequests = 4
            }));

        await service.SearchAsync(new TvShowSearchRequest("breaking", 1, 20));

        Assert.True(provider.LastIncludeKeywords);
    }

    private sealed class KeywordAwareTvShowDataProvider : ITvShowDataProvider
    {
        public bool LastIncludeKeywords { get; private set; }

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShowProviderSearchResult(
                [
                    new TvShowProviderSummary(
                        "fake-tv-1",
                        1,
                        null,
                        null,
                        "Show",
                        "Show",
                        "Overview",
                        new DateOnly(2020, 1, 1),
                        "/poster.jpg",
                        null,
                        "en",
                        8m,
                        100)
                ],
                page,
                pageSize,
                1,
                1));

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default)
        {
            LastIncludeKeywords = includeKeywords;
            return Task.FromResult<TvShowProviderDetails?>(new TvShowProviderDetails(
                externalId,
                1,
                null,
                null,
                "Show",
                "Show",
                "Overview",
                new DateOnly(2020, 1, 1),
                null,
                "/poster.jpg",
                null,
                "en",
                8m,
                100,
                "Ended",
                ["Drama"],
                [],
                null,
                [new ProviderKeywordSummary(5, "crime")]));
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

    private sealed class NoOpTvShowRepository : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShow
            {
                Id = Guid.NewGuid(),
                TmdbId = details.TmdbId,
                Title = details.Title
            });

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TvShow>> UpsertFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TvShow>>(
                details
                    .Select(detail => new TvShow
                    {
                        Id = Guid.NewGuid(),
                        TmdbId = detail.TmdbId,
                        Title = detail.Title
                    })
                    .ToList());
    }

    private sealed class NoOpCatalogSyncStateService : ITvShowCatalogSyncStateService
    {
        public Task MarkRefreshedAsync(
            Guid tvShowId,
            TvShowCatalogRefreshReason reason,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkRefreshedBatchAsync(
            IReadOnlyList<Guid> tvShowIds,
            TvShowCatalogRefreshReason reason,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkChangeSignalAsync(
            Guid tvShowId,
            DateOnly changeSignalDate,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkChangesSyncAsync(
            Guid tvShowId,
            DateTime refreshedAtUtc,
            DateOnly changeSignalDate,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkHotReleaseAsync(
            Guid tvShowId,
            DateTime refreshedAtUtc,
            DateTime? nextHotCheckAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateNextHotCheckAsync(
            Guid tvShowId,
            DateTime? nextHotCheckAtUtc,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
