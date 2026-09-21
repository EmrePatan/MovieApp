using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Contracts.Auth;
using MovieApp.IntegrationTests.Auth;
using MovieApp.Contracts.TvShowFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.TvShowFollows;

[Collection("TvShowFollowsApi")]
public sealed class TvShowFollowBaselineApiTests(TvShowFollowsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task FollowCreationEstablishesBaseline()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedBreakingBadAsync(fullyHydrated: true);
        var token = await RegisterAndGetTokenAsync("baseline-create-user");

        var response = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<TvShowFollowStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status.BaselineEstablished);

        await using var context = TvShowFollowsApiFixture.CreateContext();
        var follow = await context.CatalogFollows.SingleAsync();
        Assert.NotNull(follow.NotifyFromUtc);
        Assert.NotNull(follow.BaselineEstablishedAtUtc);
    }

    [Fact]
    public async Task BaselineAbsorbsHistoricalEpisodesWithoutNotifications()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedBreakingBadAsync(fullyHydrated: true);
        var token = await RegisterAndGetTokenAsync("baseline-absorb-user");

        await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        await using var context = TvShowFollowsApiFixture.CreateContext();
        Assert.True(await context.CatalogReleaseEvents.AnyAsync());
        Assert.False(await context.UserReleaseNotifications.AnyAsync());
        Assert.False(await context.UserReleaseNotificationEvents.AnyAsync());
    }

    [Fact]
    public async Task FullyHydratedShowUsesZeroProviderSeasonCalls()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedBreakingBadAsync(fullyHydrated: true);
        fixture.ResetProviderTracker();
        var token = await RegisterAndGetTokenAsync("baseline-zero-provider-user");

        await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        using var scope = fixture.Factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        Assert.Equal(0, tracker.GetTvShowCallCount);
        Assert.Equal(0, tracker.GetSeasonCallCount);
    }

    [Fact]
    public async Task MultipleHistoricalSeasonsCanEstablishBaselineWithoutConcurrencyFailure()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedMultiSeasonPartialShowAsync(seasonCount: 3);
        fixture.ResetProviderTracker();
        var token = await RegisterAndGetTokenAsync("baseline-multi-season-user");

        var response = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<TvShowFollowStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status.BaselineEstablished);

        using var scope = fixture.Factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        Assert.Equal(3, tracker.GetSeasonCallCount);

        await using var context = TvShowFollowsApiFixture.CreateContext();
        var hydratedEpisodeCount = await context.Episodes
            .Where(episode => episode.Season!.TvShowId == tvShowId)
            .CountAsync();
        Assert.True(hydratedEpisodeCount > 0);
    }

    [Fact]
    public async Task PartialHistoricalSeasonIsHydratedBeforeBaseline()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedBreakingBadAsync(
            fullyHydrated: false,
            season1PersistedEpisodes: 1);
        fixture.ResetProviderTracker();
        var token = await RegisterAndGetTokenAsync("baseline-partial-user");

        await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        using var scope = fixture.Factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        Assert.Equal(0, tracker.GetTvShowCallCount);
        Assert.Equal(1, tracker.GetSeasonCallCount);
    }

    [Fact]
    public async Task FutureSeasonIsNotHydratedForBaseline()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedBreakingBadAsync(
            fullyHydrated: true,
            includeFutureSeasonSummary: true);
        fixture.ResetProviderTracker();
        var token = await RegisterAndGetTokenAsync("baseline-future-season-user");

        await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        using var scope = fixture.Factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        Assert.Equal(0, tracker.GetSeasonCallCount);
    }

    [Fact]
    public async Task FailedBaselineCanRetryWithoutChangingNotifyFromUtc()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedBreakingBadAsync(
            fullyHydrated: false,
            season1PersistedEpisodes: 0);
        var token = await RegisterAndGetTokenAsync("baseline-retry-user");

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>().FailGetSeason = true;
        }

        var failedResponse = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);

        DateTime? originalNotifyFromUtc;
        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var follow = await context.CatalogFollows.SingleAsync();
            originalNotifyFromUtc = follow.NotifyFromUtc;
            Assert.NotNull(originalNotifyFromUtc);
            Assert.Null(follow.BaselineEstablishedAtUtc);
        }

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>().FailGetSeason = false;
        }

        var retryResponse = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);

        var retryStatus = await retryResponse.Content.ReadFromJsonAsync<TvShowFollowStatusResponse>();
        Assert.NotNull(retryStatus);
        Assert.True(retryStatus.BaselineEstablished);

        await using var verifyContext = TvShowFollowsApiFixture.CreateContext();
        var verifiedFollow = await verifyContext.CatalogFollows.SingleAsync();
        Assert.Equal(originalNotifyFromUtc, verifiedFollow.NotifyFromUtc);
        Assert.NotNull(verifiedFollow.BaselineEstablishedAtUtc);
    }

    [Fact]
    public async Task ConcurrentFollowRequestsKeepSingleStableBaseline()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedBreakingBadAsync(fullyHydrated: true);
        var token = await RegisterAndGetTokenAsync("baseline-concurrent-user");

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => SendAuthorizedPutAsync(
                $"/api/tvshows/{tvShowId}/follow",
                token,
                new UpsertTvShowFollowRequest(true, true)))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);

        await using var context = TvShowFollowsApiFixture.CreateContext();
        var follows = await context.CatalogFollows.ToListAsync();
        Assert.Single(follows);
        Assert.NotNull(follows[0].NotifyFromUtc);
        Assert.NotNull(follows[0].BaselineEstablishedAtUtc);
    }

    [Fact]
    public async Task EstablishedFollowPreferenceUpdateDoesNotRebaseline()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedBreakingBadAsync(fullyHydrated: true);
        var token = await RegisterAndGetTokenAsync("baseline-no-rebaseline-user");

        await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        fixture.ResetProviderTracker();

        var updateResponse = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(false, null));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        Assert.Equal(0, tracker.GetTvShowCallCount);
        Assert.Equal(0, tracker.GetSeasonCallCount);
    }

    [Fact]
    public async Task SeasonZeroOnlyShowCanEstablishBaseline()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedSeasonZeroOnlyShowAsync();
        var token = await RegisterAndGetTokenAsync("baseline-season-zero-user");

        var response = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<TvShowFollowStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status.BaselineEstablished);

        await using var context = TvShowFollowsApiFixture.CreateContext();
        Assert.Equal(0, await context.CatalogReleaseEvents.CountAsync());
    }

    private static async Task<Guid> SeedBreakingBadAsync(
        bool fullyHydrated = false,
        int? season1PersistedEpisodes = null,
        bool includeFutureSeasonSummary = false)
    {
        await using var context = TvShowFollowsApiFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var tvShow = new TvShow
        {
            Id = tvShowId,
            TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);

        var season1 = CreateSeason(tvShowId, 1, new DateOnly(2008, 1, 20), 3);
        context.Seasons.Add(season1);

        if (fullyHydrated || season1PersistedEpisodes > 0)
        {
            var episodeCount = fullyHydrated ? 3 : season1PersistedEpisodes!.Value;
            for (var episodeNumber = 1; episodeNumber <= episodeCount; episodeNumber++)
            {
                context.Episodes.Add(CreateEpisode(
                    season1.Id,
                    episodeNumber,
                    new DateOnly(2008, 1, 20).AddDays((episodeNumber - 1) * 7)));
            }
        }

        if (fullyHydrated)
        {
            var season2 = CreateSeason(tvShowId, 2, new DateOnly(2009, 3, 8), 2);
            context.Seasons.Add(season2);
            context.Episodes.Add(CreateEpisode(season2.Id, 1, new DateOnly(2009, 3, 8)));
            context.Episodes.Add(CreateEpisode(season2.Id, 2, new DateOnly(2009, 3, 15)));
        }

        if (includeFutureSeasonSummary)
        {
            context.Seasons.Add(CreateSeason(tvShowId, 3, new DateOnly(2030, 1, 1), 2));
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task<Guid> SeedMultiSeasonPartialShowAsync(int seasonCount)
    {
        await using var context = TvShowFollowsApiFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
            Title = "Multi Season Partial",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        for (var seasonNumber = 1; seasonNumber <= seasonCount; seasonNumber++)
        {
            context.Seasons.Add(CreateSeason(
                tvShowId,
                seasonNumber,
                new DateOnly(2008, 1, 20).AddMonths(seasonNumber - 1),
                3));
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task<Guid> SeedSeasonZeroOnlyShowAsync()
    {
        await using var context = TvShowFollowsApiFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = "Specials Only",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        context.Seasons.Add(CreateSeason(tvShowId, 0, new DateOnly(2008, 1, 1), 5));
        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static Season CreateSeason(Guid tvShowId, int seasonNumber, DateOnly airDate, int episodeCount) =>
        new()
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShowId,
            SeasonNumber = seasonNumber,
            AirDate = airDate,
            EpisodeCount = episodeCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Episode CreateEpisode(Guid seasonId, int episodeNumber, DateOnly airDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = episodeNumber,
            AirDate = airDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private Task<string> RegisterAndGetTokenAsync(string username) =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"{username}@example.com");

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
