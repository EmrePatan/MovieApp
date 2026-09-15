using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.TvShows;

public sealed class GetTvShowByIdServiceActionEligibilityTests
{
    private static readonly Guid TvShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetByIdAsync_ReturningSeries_AllowsFollow()
    {
        var service = CreateService(TvShowStatus.ReturningSeries);

        var result = await service.GetByIdAsync(TvShowId);

        Assert.True(result.CanFollow);
    }

    [Fact]
    public async Task GetByIdAsync_Ended_DisallowsFollow()
    {
        var service = CreateService(TvShowStatus.Ended);

        var result = await service.GetByIdAsync(TvShowId);

        Assert.False(result.CanFollow);
    }

    private static GetTvShowByIdService CreateService(TvShowStatus status) =>
        new(
            new FakeSeasonSummaryHydrator(status),
            new NoOpCatalogSyncStateService(),
            new NoOpCacheService());

    private sealed class FakeSeasonSummaryHydrator(TvShowStatus status) : ITvShowSeasonSummaryHydrator
    {
        public Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            var tvShow = new TvShow
            {
                Id = tvShowId,
                Title = "Show",
                Status = status,
                LastAirDate = new DateOnly(2026, 9, 1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Seasons =
                [
                    new Season
                    {
                        Id = Guid.NewGuid(),
                        TvShowId = tvShowId,
                        SeasonNumber = 1,
                        Name = "Season 1",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                ]
            };

            return Task.FromResult(new TvShowSeasonSummaryHydrationResult(tvShow, ProviderCatalogRefreshed: false));
        }
    }

    private sealed class NoOpCatalogSyncStateService : ITvShowCatalogSyncStateService
    {
        public Task MarkRefreshedAsync(
            Guid tvShowId,
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

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
