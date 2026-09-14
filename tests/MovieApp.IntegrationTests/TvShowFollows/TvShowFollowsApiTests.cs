using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Favorites;
using MovieApp.Contracts.TvShows;
using MovieApp.Contracts.TvShowFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.IntegrationTests.TvShowFollows;

[CollectionDefinition("TvShowFollowsApi")]
public sealed class TvShowFollowsApiTestsFixture : ICollectionFixture<TvShowFollowsApiFixture>;

[Collection("TvShowFollowsApi")]
public sealed class TvShowFollowsApiTests(TvShowFollowsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task UserCanFollowUpdateUnfollowAndListTvShows()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("follow-user");
        var tvShowId = await SeedTvShowAsync();

        var initialStatusResponse = await SendAuthorizedGetAsync($"/api/tvshows/{tvShowId}/follow", token);
        Assert.Equal(HttpStatusCode.OK, initialStatusResponse.StatusCode);

        var initialStatus = await initialStatusResponse.Content.ReadFromJsonAsync<TvShowFollowStatusResponse>();
        Assert.NotNull(initialStatus);
        Assert.False(initialStatus.IsFollowing);
        Assert.False(initialStatus.BaselineEstablished);

        var createResponse = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(null, null));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdStatus = await createResponse.Content.ReadFromJsonAsync<TvShowFollowStatusResponse>();
        Assert.NotNull(createdStatus);
        Assert.True(createdStatus.IsFollowing);
        Assert.True(createdStatus.NotifyNewSeasons);
        Assert.True(createdStatus.NotifyNewEpisodes);
        Assert.True(createdStatus.BaselineEstablished);

        var updateResponse = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(false, true));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedStatus = await updateResponse.Content.ReadFromJsonAsync<TvShowFollowStatusResponse>();
        Assert.NotNull(updatedStatus);
        Assert.False(updatedStatus.NotifyNewSeasons);
        Assert.True(updatedStatus.NotifyNewEpisodes);

        var listResponse = await SendAuthorizedGetAsync("/api/follows/tvshows?page=1&pageSize=20", token);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<TvShowFollowsResponse>();
        Assert.NotNull(list);
        Assert.Single(list.TvShows);
        Assert.True(list.TvShows[0].BaselineEstablished);

        var deleteResponse = await SendAuthorizedDeleteAsync($"/api/tvshows/{tvShowId}/follow", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var finalStatusResponse = await SendAuthorizedGetAsync($"/api/tvshows/{tvShowId}/follow", token);
        var finalStatus = await finalStatusResponse.Content.ReadFromJsonAsync<TvShowFollowStatusResponse>();
        Assert.NotNull(finalStatus);
        Assert.False(finalStatus.IsFollowing);
    }

    [Fact]
    public async Task FollowIsIndependentFromFavorite()
    {
        await fixture.ResetAsync();

        var token = await RegisterAndGetTokenAsync("follow-favorite-user");
        var tvShowId = await SeedTvShowAsync();

        var followResponse = await SendAuthorizedPutAsync(
            $"/api/tvshows/{tvShowId}/follow",
            token,
            new UpsertTvShowFollowRequest(true, true));
        Assert.Equal(HttpStatusCode.Created, followResponse.StatusCode);

        var favoriteResponse = await SendAuthorizedPostAsync(_client, $"/api/favorites/tvshows/{tvShowId}", token);
        Assert.Equal(HttpStatusCode.Created, favoriteResponse.StatusCode);

        await SendAuthorizedDeleteAsync($"/api/tvshows/{tvShowId}/follow", token);

        var favoriteStatusResponse = await SendAuthorizedGetAsync(
            $"/api/favorites/tvshows/{tvShowId}/status",
            token);
        var favoriteStatus = await favoriteStatusResponse.Content.ReadFromJsonAsync<FavoriteStatusResponse>();
        Assert.NotNull(favoriteStatus);
        Assert.True(favoriteStatus.IsFavorited);
    }

    [Fact]
    public async Task ConcurrentFollowCreateKeepsSingleRow()
    {
        const int iterationCount = 15;

        for (var iteration = 0; iteration < iterationCount; iteration++)
        {
            await fixture.ResetAsync();

            var token = await RegisterAndGetTokenAsync($"concurrent-follow-user-{iteration}");
            var tvShowId = await SeedTvShowAsync();

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => SendAuthorizedPutAsync(
                    $"/api/tvshows/{tvShowId}/follow",
                    token,
                    new UpsertTvShowFollowRequest(true, true)))
                .ToArray();

            var responses = await Task.WhenAll(tasks);

            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.All(responses, response =>
                Assert.True(
                    response.StatusCode == HttpStatusCode.Created ||
                    response.StatusCode == HttpStatusCode.OK,
                    $"Iteration {iteration} returned unexpected status {response.StatusCode}"));

            await using var context = TvShowFollowsApiFixture.CreateContext();
            var follow = await context.CatalogFollows.SingleAsync();
            Assert.NotNull(follow.NotifyFromUtc);
            Assert.NotNull(follow.BaselineEstablishedAtUtc);
        }
    }

    [Fact]
    public async Task SchemaEnforcesNotificationAndReleaseConstraints()
    {
        await fixture.ResetAsync();

        Guid userId;
        Guid tvShowId;
        Guid releaseEventId;
        Guid notificationId;

        await using (var seedContext = TvShowFollowsApiFixture.CreateContext())
        {
            var user = User.Create(
                Guid.NewGuid(),
                "schema-user@example.com",
                "hash",
                "Schema User",
                DateTime.UtcNow);
            var tvShow = new TvShow
            {
                Id = Guid.NewGuid(),
                Title = "Schema Show",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            seedContext.Users.Add(user);
            seedContext.TvShows.Add(tvShow);

            var dedupeKey = CatalogReleaseEventDedupeKey.ForEpisode(tvShow.Id, 1, 1);
            var releaseEvent = new CatalogReleaseEvent
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShow.Id,
                EventType = CatalogReleaseEventType.NewEpisode,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                ReleaseAtUtc = DateTime.UtcNow,
                DetectedAtUtc = DateTime.UtcNow,
                Source = CatalogReleaseEventSource.BoundaryDetection,
                DedupeKey = dedupeKey
            };

            var notification = new UserReleaseNotification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TvShowId = tvShow.Id,
                NotificationType = UserReleaseNotificationType.NewEpisodes,
                Status = UserReleaseNotificationStatus.Pending,
                AggregationWindowKey = "2026-09-14T18:00:00Z",
                CreatedAtUtc = DateTime.UtcNow
            };

            seedContext.CatalogReleaseEvents.Add(releaseEvent);
            seedContext.UserReleaseNotifications.Add(notification);
            await seedContext.SaveChangesAsync();

            userId = user.Id;
            tvShowId = tvShow.Id;
            releaseEventId = releaseEvent.Id;
            notificationId = notification.Id;
        }

        await using (var duplicateReleaseContext = TvShowFollowsApiFixture.CreateContext())
        {
            duplicateReleaseContext.CatalogReleaseEvents.Add(new CatalogReleaseEvent
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShowId,
                EventType = CatalogReleaseEventType.NewEpisode,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                ReleaseAtUtc = DateTime.UtcNow,
                DetectedAtUtc = DateTime.UtcNow,
                Source = CatalogReleaseEventSource.BoundaryDetection,
                DedupeKey = CatalogReleaseEventDedupeKey.ForEpisode(tvShowId, 1, 1)
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateReleaseContext.SaveChangesAsync());
        }

        await using (var junctionContext = TvShowFollowsApiFixture.CreateContext())
        {
            junctionContext.UserReleaseNotificationEvents.Add(new UserReleaseNotificationEvent
            {
                UserReleaseNotificationId = notificationId,
                CatalogReleaseEventId = releaseEventId,
                UserId = userId
            });
            await junctionContext.SaveChangesAsync();
        }

        await using (var duplicateJunctionContext = TvShowFollowsApiFixture.CreateContext())
        {
            duplicateJunctionContext.UserReleaseNotificationEvents.Add(new UserReleaseNotificationEvent
            {
                UserReleaseNotificationId = notificationId,
                CatalogReleaseEventId = releaseEventId,
                UserId = userId
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateJunctionContext.SaveChangesAsync());
        }

        await using (var syncStateContext = TvShowFollowsApiFixture.CreateContext())
        {
            syncStateContext.TvShowCatalogSyncStates.Add(new Domain.Entities.TvShowCatalogSyncState
            {
                TvShowId = tvShowId,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await syncStateContext.SaveChangesAsync();
        }

        await using (var duplicateSyncStateContext = TvShowFollowsApiFixture.CreateContext())
        {
            duplicateSyncStateContext.TvShowCatalogSyncStates.Add(new Domain.Entities.TvShowCatalogSyncState
            {
                TvShowId = tvShowId,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateSyncStateContext.SaveChangesAsync());
        }

        await using (var checkpointContext = TvShowFollowsApiFixture.CreateContext())
        {
            checkpointContext.TmdbTvChangesSyncCheckpoints.Add(new TmdbTvChangesSyncCheckpoint
            {
                CheckpointKey = TmdbTvChangesSyncCheckpoint.DefaultCheckpointKey,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await checkpointContext.SaveChangesAsync();
        }

        await using (var duplicateCheckpointContext = TvShowFollowsApiFixture.CreateContext())
        {
            duplicateCheckpointContext.TmdbTvChangesSyncCheckpoints.Add(new TmdbTvChangesSyncCheckpoint
            {
                CheckpointKey = TmdbTvChangesSyncCheckpoint.DefaultCheckpointKey,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateCheckpointContext.SaveChangesAsync());
        }
    }

    private async Task<string> RegisterAndGetTokenAsync(string username)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                $"{username}@example.com",
                "Password123!",
                username));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth.AccessToken;
    }

    private static async Task<Guid> SeedTvShowAsync()
    {
        await using var context = TvShowFollowsApiFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = "Follow Integration Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return tvShowId;
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync<TPayload>(
        string url,
        string token,
        TPayload payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedPostAsync(
        HttpClient client,
        string url,
        string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
