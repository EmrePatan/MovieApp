using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Application.Services.StreamingLeaving;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.StreamingLeaving;

public sealed class StreamingLeavingFetchServiceTests
{
    private static readonly DateTime Start = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly StreamingAvailabilityOptions _options = new() { Enabled = true, ApiKey = "test" };
    private readonly InMemoryRepository _repository = new();
    private readonly StubProvider _provider = new();
    private readonly StubLock _lock = new();
    private readonly RecordingCache _cache = new();
    private readonly Clock _clock = new(Start);

    [Fact]
    public async Task OneFetchStopsAtTwentyRequestsAndStoresTheCutList()
    {
        _provider.PageTotal = 100;

        await FetchAsync("US");

        Assert.Equal(20, _provider.Requests);
        Assert.Equal(20, _repository.Usage["US"]);
        Assert.Equal(19, _repository.Items["US"].Count);
        Assert.False(_repository.Regions["US"].IsComplete);
        Assert.Equal(Start, _repository.Regions["US"].LastSuccessAtUtc);
    }

    [Fact]
    public async Task AFetchDropsTheCachedListOfItsRegion()
    {
        await FetchAsync("TR");

        Assert.Equal(["streaming-leaving:list:TR"], _cache.RemovedKeys);
    }

    [Fact]
    public async Task NoSecondFetchBeforeThreeDays()
    {
        await FetchAsync("TR");
        var spent = _provider.Requests;

        _clock.Now = Start.AddDays(3).AddMinutes(-1);
        await FetchAsync("TR");
        Assert.Equal(spent, _provider.Requests);

        _clock.Now = Start.AddDays(3);
        await FetchAsync("TR");
        // The service list is cached, so only the pages are asked again.
        Assert.Equal(spent + _provider.PageTotal, _provider.Requests);
    }

    [Fact]
    public async Task ARunningFetchForTheRegionBlocksAnother()
    {
        _lock.Held = true;

        await FetchAsync("TR");

        Assert.Equal(0, _provider.Requests);
    }

    [Fact]
    public async Task OtherRegionsStopAtSevenHundredNinetyAndTurkeyKeepsItsShare()
    {
        _repository.Usage["US"] = 789;
        _provider.PageTotal = 10;

        await FetchAsync("DE");
        Assert.Equal(1, _provider.Requests);

        await FetchAsync("FR");
        Assert.Equal(1, _provider.Requests);

        await FetchAsync("TR");
        Assert.Equal(12, _provider.Requests);
    }

    [Fact]
    public async Task NothingIsSentWhenTheMonthIsSpent()
    {
        _repository.Usage["TR"] = 110;
        _repository.Usage["US"] = 790;

        await FetchAsync("TR");

        Assert.Equal(0, _provider.Requests);
        Assert.False(_repository.Regions.ContainsKey("TR"));
    }

    [Fact]
    public async Task AFailedFetchKeepsTheOldListCountsTheRequestAndWaits()
    {
        await FetchAsync("TR");
        var stored = _repository.Items["TR"];
        var spent = _repository.Usage["TR"];

        _clock.Now = Start.AddDays(3);
        _provider.PageTotal = 5;
        _provider.FailOnPage = 2;
        await FetchAsync("TR");

        Assert.Same(stored, _repository.Items["TR"]);
        Assert.Equal(spent + 2, _repository.Usage["TR"]);
        Assert.Equal(Start, _repository.Regions["TR"].LastSuccessAtUtc);

        var requests = _provider.Requests;
        _clock.Now = _clock.Now.AddHours(5);
        await FetchAsync("TR");
        Assert.Equal(requests, _provider.Requests);

        _provider.FailOnPage = null;
        _clock.Now = _clock.Now.AddHours(1);
        await FetchAsync("TR");
        Assert.True(_provider.Requests > requests);
    }

    [Fact]
    public async Task APartialListIsStoredOnlyWhenThereIsNoOldOne()
    {
        _provider.PageTotal = 5;
        _provider.FailOnPage = 3;

        await FetchAsync("TR");

        Assert.Equal(2, _repository.Items["TR"].Count);
        Assert.Null(_repository.Regions["TR"].LastSuccessAtUtc);
    }

    [Fact]
    public async Task ASpentMonthKeepsTheOldListAndIsNotASuccess()
    {
        await FetchAsync("TR");
        var stored = _repository.Items["TR"];

        _clock.Now = Start.AddDays(3);
        _provider.PageTotal = 5;
        _repository.Usage["US"] = 900 - _repository.Usage["TR"] - 2;
        await FetchAsync("TR");

        Assert.Same(stored, _repository.Items["TR"]);
        Assert.Equal(Start, _repository.Regions["TR"].LastSuccessAtUtc);
    }

    [Fact]
    public async Task ASpentMonthStoresThePartialListOfANewRegionWithoutSuccess()
    {
        _repository.Usage["US"] = 787;
        _provider.PageTotal = 10;

        await FetchAsync("DE");

        Assert.Equal(2, _repository.Items["DE"].Count);
        Assert.Null(_repository.Regions["DE"].LastSuccessAtUtc);
    }

    [Fact]
    public async Task ARegionWithoutDataIsNotAskedAgainForThreeDays()
    {
        _provider.Services = [];

        await FetchAsync("XK");
        _clock.Now = Start.AddDays(2);
        await FetchAsync("XK");

        Assert.Equal(1, _provider.Requests);
        Assert.Empty(_repository.Items["XK"]);
    }

    [Fact]
    public async Task NothingIsSentWithoutAKey()
    {
        _options.ApiKey = string.Empty;

        await FetchAsync("TR");

        Assert.Equal(0, _provider.Requests);
    }

    private Task FetchAsync(string region) =>
        new StreamingLeavingFetchService(
            _repository,
            _provider,
            new StubResolver(),
            _lock,
            _cache,
            Options.Create(_options),
            _clock,
            NullLogger<StreamingLeavingFetchService>.Instance).FetchAsync(region);

    private sealed class RecordingCache : ICacheService
    {
        public List<string> RemovedKeys { get; } = [];

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

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class StubProvider : IStreamingAvailabilityProvider
    {
        public IReadOnlyList<StreamingService> Services { get; set; } = [new("netflix", "Netflix")];

        public int PageTotal { get; set; } = 2;

        public int? FailOnPage { get; set; }

        public int Requests { get; private set; }

        public Task<IReadOnlyList<StreamingService>> GetSubscriptionServicesAsync(
            string region,
            CancellationToken cancellationToken = default)
        {
            Requests++;
            return Task.FromResult(Services);
        }

        public Task<StreamingLeavingPage> GetExpiringAsync(
            string region,
            IReadOnlyList<string> serviceIds,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            Requests++;
            var page = cursor is null ? 1 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
            if (page == FailOnPage)
            {
                throw new HttpRequestException("boom");
            }

            var change = new StreamingLeavingChange(
                "netflix",
                new StreamingTitleKey(CatalogContentType.Movie, page),
                Start.AddDays(20),
                null);
            var next = page < PageTotal
                ? (page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
            return Task.FromResult(new StreamingLeavingPage([change], next));
        }
    }

    private sealed class StubResolver : IStreamingLeavingTitleResolver
    {
        public Task<IReadOnlyDictionary<StreamingTitleKey, Guid>> ResolveAsync(
            IReadOnlyList<StreamingTitleKey> titles,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<StreamingTitleKey, Guid>>(
                titles.ToDictionary(title => title, _ => Guid.NewGuid()));
    }

    private sealed class StubLock : ISearchRefreshLockService
    {
        public bool Held { get; set; }

        public Task<SearchRefreshLockHandle?> TryAcquireAsync(
            string lockKey,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Held
                ? null
                : new SearchRefreshLockHandle(lockKey, "token", SearchRefreshLockBackend.LocalSingleFlight));

        public Task ReleaseAsync(
            string lockKey,
            string lockToken,
            SearchRefreshLockBackend backend,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TryRenewAsync(
            string lockKey,
            string lockToken,
            SearchRefreshLockBackend backend,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class InMemoryRepository : IStreamingLeavingRepository
    {
        public Dictionary<string, StreamingLeavingRegion> Regions { get; } = [];

        public Dictionary<string, int> Usage { get; } = [];

        public Dictionary<string, IReadOnlyList<StreamingLeavingItem>> Items { get; } = [];

        public Task<StreamingLeavingRegion?> GetRegionAsync(
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Regions.TryGetValue(region, out var state) ? Copy(state) : null);

        public Task SaveRegionAsync(StreamingLeavingRegion state, CancellationToken cancellationToken = default)
        {
            Regions[state.Region] = Copy(state);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, int>> GetUsageAsync(
            DateOnly periodStart,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>(Usage));

        public Task<bool> TryReserveRequestAsync(
            DateOnly periodStart,
            string region,
            Func<IReadOnlyDictionary<string, int>, bool> isAllowed,
            CancellationToken cancellationToken = default)
        {
            if (!isAllowed(Usage))
            {
                return Task.FromResult(false);
            }

            Usage[region] = Usage.GetValueOrDefault(region) + 1;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<StreamingLeavingRow>> GetUpcomingAsync(
            string region,
            DateTime nowUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> HasUpcomingAsync(
            string region,
            DateTime nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.TryGetValue(region, out var items) && items.Any(item => item.LeavesAtUtc > nowUtc));

        public Task ReplaceItemsAsync(
            string region,
            IReadOnlyList<StreamingLeavingItem> items,
            CancellationToken cancellationToken = default)
        {
            Items[region] = items;
            return Task.CompletedTask;
        }

        private static StreamingLeavingRegion Copy(StreamingLeavingRegion state) =>
            new()
            {
                Region = state.Region,
                LastAttemptAtUtc = state.LastAttemptAtUtc,
                LastSuccessAtUtc = state.LastSuccessAtUtc,
                LastFetchRequestCount = state.LastFetchRequestCount,
                IsComplete = state.IsComplete,
                ServicesJson = state.ServicesJson,
                ServicesFetchedAtUtc = state.ServicesFetchedAtUtc
            };
    }
}
