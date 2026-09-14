using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.TvShowFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.TvShowCatalogSyncState;

[CollectionDefinition("TvShowCatalogSyncState")]
public sealed class TvShowCatalogSyncStateCollection : ICollectionFixture<TvShowCatalogSyncStateFixture>;

[Collection("TvShowCatalogSyncState")]
public sealed class TvShowCatalogSyncStateIntegrationTests(TvShowCatalogSyncStateFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task ProviderHydrationCreatesCatalogSyncState()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithoutSeasonsAsync();

        var response = await _client.GetAsync($"/api/tvshows/{tvShowId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(tvShowId, syncState.TvShowId);
        Assert.NotNull(syncState.LastRefreshedAtUtc);
        Assert.Equal(TvShowCatalogRefreshReason.DetailHydration, syncState.LastRefreshReason);
    }

    [Fact]
    public async Task DbOnlyReadDoesNotCreateCatalogSyncState()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedFullyHydratedShowAsync();

        var response = await _client.GetAsync($"/api/tvshows/{tvShowId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        Assert.Equal(0, await context.TvShowCatalogSyncStates.CountAsync());
    }

    [Fact]
    public async Task FollowBaselineProviderHydrationUsesFollowBaselineReason()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithPartialSeasonAsync();
        var token = await RegisterAndGetTokenAsync("catalog-sync-baseline-user");

        var response = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(TvShowCatalogRefreshReason.FollowBaseline, syncState.LastRefreshReason);
        Assert.NotNull(syncState.LastRefreshedAtUtc);
    }

    [Fact]
    public async Task FullyHydratedBaselineDoesNotFakeRefresh()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedFullyHydratedShowAsync();
        var token = await RegisterAndGetTokenAsync("catalog-sync-no-fake-refresh-user");

        var response = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        Assert.Equal(0, await context.TvShowCatalogSyncStates.CountAsync());
    }

    [Fact]
    public async Task FailedProviderCallDoesNotAdvanceSyncState()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithSeasonSummaryOnlyAsync();

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
            tracker.FailGetSeason = true;
        }

        var response = await _client.GetAsync($"/api/tvshows/{tvShowId}/seasons/1");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        Assert.Equal(0, await context.TvShowCatalogSyncStates.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRefreshesKeepSingleNewestState()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithSeasonSummaryOnlyAsync();
        var olderTimestamp = new DateTime(2026, 9, 14, 20, 0, 0, DateTimeKind.Utc);
        var newerTimestamp = new DateTime(2026, 9, 14, 20, 1, 0, DateTimeKind.Utc);

        var tasks = Enumerable.Range(0, 5)
            .Select(index =>
            {
                var reason = index % 2 == 0
                    ? TvShowCatalogRefreshReason.DetailHydration
                    : TvShowCatalogRefreshReason.FollowBaseline;
                var timestamp = index % 2 == 0 ? newerTimestamp : olderTimestamp;

                return Task.Run(async () =>
                {
                    using var scope = fixture.Factory.Services.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<ITvShowCatalogSyncStateRepository>();
                    await repository.MarkRefreshedAsync(tvShowId, reason, timestamp);
                });
            })
            .ToArray();

        await Task.WhenAll(tasks);

        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(newerTimestamp, syncState.LastRefreshedAtUtc);
        Assert.Equal(TvShowCatalogRefreshReason.DetailHydration, syncState.LastRefreshReason);
    }

    [Fact]
    public async Task ExistingChangeSignalAndHotCheckFieldsArePreservedWhenMarkingRefresh()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithoutSeasonsAsync();
        var changeSignalDate = new DateOnly(2026, 9, 10);
        var nextHotCheckAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

        await using (var seedContext = TvShowCatalogSyncStateFixture.CreateContext())
        {
            seedContext.TvShowCatalogSyncStates.Add(new Domain.Entities.TvShowCatalogSyncState
            {
                TvShowId = tvShowId,
                LastChangeSignalDate = changeSignalDate,
                NextHotCheckAtUtc = nextHotCheckAtUtc,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var syncStateService = scope.ServiceProvider.GetRequiredService<ITvShowCatalogSyncStateService>();
            await syncStateService.MarkRefreshedAsync(
                tvShowId,
                TvShowCatalogRefreshReason.DetailHydration,
                new DateTime(2026, 9, 14, 21, 0, 0, DateTimeKind.Utc));
        }

        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(changeSignalDate, syncState.LastChangeSignalDate);
        Assert.Equal(nextHotCheckAtUtc, syncState.NextHotCheckAtUtc);
        Assert.Equal(TvShowCatalogRefreshReason.DetailHydration, syncState.LastRefreshReason);
        Assert.NotNull(syncState.LastRefreshedAtUtc);
    }

    [Fact]
    public async Task MarkRefreshed_RejectsOlderTimestampAndKeepsNewerReason()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithoutSeasonsAsync();
        var newerTimestamp = new DateTime(2026, 9, 14, 20, 1, 0, DateTimeKind.Utc);
        var olderTimestamp = new DateTime(2026, 9, 14, 20, 0, 0, DateTimeKind.Utc);

        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITvShowCatalogSyncStateRepository>();

        await repository.MarkRefreshedAsync(tvShowId, TvShowCatalogRefreshReason.DetailHydration, newerTimestamp);
        await repository.MarkRefreshedAsync(tvShowId, TvShowCatalogRefreshReason.FollowBaseline, olderTimestamp);

        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(newerTimestamp, syncState.LastRefreshedAtUtc);
        Assert.Equal(TvShowCatalogRefreshReason.DetailHydration, syncState.LastRefreshReason);
    }

    private static async Task<Guid> SeedShowWithoutSeasonsAsync()
    {
        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task<Guid> SeedShowWithSeasonSummaryOnlyAsync()
    {
        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Seasons.Add(new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShowId,
            SeasonNumber = 1,
            AirDate = new DateOnly(2008, 1, 20),
            EpisodeCount = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task<Guid> SeedShowWithPartialSeasonAsync()
    {
        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            AirDate = new DateOnly(2008, 1, 20),
            EpisodeCount = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Episodes.Add(new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = 1,
            AirDate = new DateOnly(2008, 1, 20),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task<Guid> SeedFullyHydratedShowAsync()
    {
        await using var context = TvShowCatalogSyncStateFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var season1Id = Guid.NewGuid();
        var season2Id = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Seasons.Add(new Season
        {
            Id = season1Id,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            AirDate = new DateOnly(2008, 1, 20),
            EpisodeCount = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Seasons.Add(new Season
        {
            Id = season2Id,
            TvShowId = tvShowId,
            SeasonNumber = 2,
            AirDate = new DateOnly(2009, 3, 8),
            EpisodeCount = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        for (var episodeNumber = 1; episodeNumber <= 3; episodeNumber++)
        {
            context.Episodes.Add(new Episode
            {
                Id = Guid.NewGuid(),
                SeasonId = season1Id,
                EpisodeNumber = episodeNumber,
                AirDate = new DateOnly(2008, 1, 20).AddDays((episodeNumber - 1) * 7),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        for (var episodeNumber = 1; episodeNumber <= 2; episodeNumber++)
        {
            context.Episodes.Add(new Episode
            {
                Id = Guid.NewGuid(),
                SeasonId = season2Id,
                EpisodeNumber = episodeNumber,
                AirDate = new DateOnly(2009, 3, 8).AddDays((episodeNumber - 1) * 7),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }

    private async Task<string> RegisterAndGetTokenAsync(string username)
    {
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                $"{username}@example.com",
                "Password123!",
                username));

        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth.AccessToken;
    }

    private Task<HttpResponseMessage> SendAuthorizedPutAsync(
        string url,
        string token,
        UpsertTvShowFollowRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(message);
    }
}
