using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.CatalogFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.IntegrationTests.TvShowFollows;

[Collection("TvShowFollowsApi")]
public sealed class CatalogUpcomingApiTests(TvShowFollowsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetUpcomingCatalog_ReturnsMixedMovieAndTvOrderedByNearestDate()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movieId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            context.Movies.AddRange(
                new Movie
                {
                    Id = movieId,
                    Title = "Future Movie",
                    PosterPath = "/future-movie.jpg",
                    ReleaseDate = today.AddDays(10),
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                },
                new Movie
                {
                    Id = Guid.NewGuid(),
                    Title = "Past Movie",
                    ReleaseDate = today.AddDays(-1),
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                },
                new Movie
                {
                    Id = Guid.NewGuid(),
                    Title = "Undated Movie",
                    ReleaseDate = null,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            context.TvShows.AddRange(
                new TvShow
                {
                    Id = tvShowId,
                    Title = "Future Show",
                    PosterPath = "/future-show.jpg",
                    FirstAirDate = today.AddDays(5),
                    Status = TvShowStatus.ReturningSeries,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                },
                new TvShow
                {
                    Id = Guid.NewGuid(),
                    Title = "Past Show",
                    FirstAirDate = today.AddDays(-3),
                    Status = TvShowStatus.Ended,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                },
                new TvShow
                {
                    Id = Guid.NewGuid(),
                    Title = "Undated Show",
                    FirstAirDate = null,
                    Status = TvShowStatus.Planned,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            await context.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/catalog/upcoming?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CatalogUpcomingResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(tvShowId, payload.Items[0].ContentId);
        Assert.Equal("Tv", payload.Items[0].ContentType);
        Assert.Equal("TvShowPremiere", payload.Items[0].UpcomingKind);
        Assert.Equal(today.AddDays(5), payload.Items[0].ReleaseDate);
        Assert.False(payload.Items[0].IsFollowed);
        Assert.Equal(movieId, payload.Items[1].ContentId);
        Assert.Equal("Movie", payload.Items[1].ContentType);
        Assert.Equal("MovieRelease", payload.Items[1].UpcomingKind);
        Assert.Equal(today.AddDays(10), payload.Items[1].ReleaseDate);
        Assert.False(payload.Items[1].IsFollowed);
    }

    [Fact]
    public async Task GetUpcomingCatalog_PaginatesWithoutDuplicatesOrGapsAcrossPages()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movieIds = Enumerable.Range(0, 5)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        var tvIds = Enumerable.Range(0, 5)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            context.Movies.AddRange(movieIds.Select((id, index) => new Movie
            {
                Id = id,
                Title = $"Future Movie {index}",
                ReleaseDate = today.AddDays(10 + index),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }));
            context.TvShows.AddRange(tvIds.Select((id, index) => new TvShow
            {
                Id = id,
                Title = $"Future Show {index}",
                FirstAirDate = today.AddDays(5 + index),
                Status = TvShowStatus.ReturningSeries,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }));
            await context.SaveChangesAsync();
        }

        var page1Response = await _client.GetAsync("/api/catalog/upcoming?page=1&pageSize=4");
        var page2Response = await _client.GetAsync("/api/catalog/upcoming?page=2&pageSize=4");

        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);

        var page1 = await page1Response.Content.ReadFromJsonAsync<CatalogUpcomingResponse>();
        var page2 = await page2Response.Content.ReadFromJsonAsync<CatalogUpcomingResponse>();

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(10, page1.TotalCount);
        Assert.Equal(4, page1.Items.Count);
        Assert.Equal(4, page2.Items.Count);

        var combined = page1.Items
            .Concat(page2.Items)
            .Select(item => (item.ContentId, item.ContentType))
            .ToList();

        Assert.Equal(8, combined.Distinct().Count());

        var expectedGlobalOrder = tvIds
            .Select((id, index) => (ContentId: id, ContentType: "Tv", ReleaseDate: today.AddDays(5 + index)))
            .Concat(movieIds.Select((id, index) => (ContentId: id, ContentType: "Movie", ReleaseDate: today.AddDays(10 + index))))
            .OrderBy(item => item.ReleaseDate)
            .ThenBy(item => item.ContentType)
            .ThenBy(item => item.ContentId)
            .ToList();

        Assert.Equal(
            expectedGlobalOrder.Take(4).Select(item => item.ContentId).ToList(),
            page1.Items.Select(item => item.ContentId).ToList());
        Assert.Equal(
            expectedGlobalOrder.Skip(4).Take(4).Select(item => item.ContentId).ToList(),
            page2.Items.Select(item => item.ContentId).ToList());
    }

    [Fact]
    public async Task GetUpcomingCatalog_UsesDeterministicOrderingForEqualReleaseDates()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sharedDate = today.AddDays(30);
        var movieA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var movieB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var tvA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var tvB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            context.Movies.AddRange(
                new Movie
                {
                    Id = movieB,
                    Title = "Movie B",
                    ReleaseDate = sharedDate,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                },
                new Movie
                {
                    Id = movieA,
                    Title = "Movie A",
                    ReleaseDate = sharedDate,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            context.TvShows.AddRange(
                new TvShow
                {
                    Id = tvB,
                    Title = "Show B",
                    FirstAirDate = sharedDate,
                    Status = TvShowStatus.ReturningSeries,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                },
                new TvShow
                {
                    Id = tvA,
                    Title = "Show A",
                    FirstAirDate = sharedDate,
                    Status = TvShowStatus.ReturningSeries,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            await context.SaveChangesAsync();
        }

        var firstResponse = await _client.GetAsync("/api/catalog/upcoming?page=1&pageSize=10");
        var secondResponse = await _client.GetAsync("/api/catalog/upcoming?page=1&pageSize=10");

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<CatalogUpcomingResponse>();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<CatalogUpcomingResponse>();

        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal(
            [movieA, movieB, tvA, tvB],
            firstPayload.Items.Select(item => item.ContentId).ToList());
        Assert.Equal(
            firstPayload.Items.Select(item => item.ContentId).ToList(),
            secondPayload.Items.Select(item => item.ContentId).ToList());
    }

    [Fact]
    public async Task GetUpcomingCatalog_WhenAuthenticated_IncludesFollowedTvEpisode()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var token = await RegisterAndGetTokenAsync("upcoming-episode-user");

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            context.TvShows.Add(new TvShow
            {
                Id = tvShowId,
                Title = "Followed Airing Show",
                PosterPath = "/followed-show.jpg",
                FirstAirDate = today.AddYears(-1),
                Status = TvShowStatus.ReturningSeries,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            context.Seasons.Add(new Season
            {
                Id = seasonId,
                TvShowId = tvShowId,
                SeasonNumber = 2,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = 3,
                Name = "The Next One",
                AirDate = today.AddDays(4),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

            var user = await context.Users.SingleAsync(user => user.UserName == "upcoming-episode-user");
            context.CatalogFollows.Add(CatalogFollow.CreateTvFollow(user.Id, tvShowId, true, true, utcNow));
            await context.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/upcoming?page=1&pageSize=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CatalogUpcomingResponse>();
        Assert.NotNull(payload);
        var episode = Assert.Single(payload.Items);
        Assert.Equal("TvEpisode", episode.UpcomingKind);
        Assert.Equal(tvShowId, episode.ContentId);
        Assert.Equal(episodeId, episode.EpisodeId);
        Assert.Equal(2, episode.SeasonNumber);
        Assert.Equal(3, episode.EpisodeNumber);
        Assert.Equal("The Next One", episode.EpisodeName);
        Assert.True(episode.IsFollowed);
    }

    [Fact]
    public async Task GetUpcomingCatalog_WhenAuthenticated_ReflectsFollowedState()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movieId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        var token = await RegisterAndGetTokenAsync("upcoming-follow-user");

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            context.Movies.Add(new Movie
            {
                Id = movieId,
                Title = "Followed Movie",
                ReleaseDate = today.AddDays(7),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            context.TvShows.Add(new TvShow
            {
                Id = tvShowId,
                Title = "Unfollowed Show",
                FirstAirDate = today.AddDays(3),
                Status = TvShowStatus.ReturningSeries,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

            var user = await context.Users.SingleAsync(user => user.UserName == "upcoming-follow-user");
            context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(user.Id, movieId, utcNow));
            await context.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/upcoming?page=1&pageSize=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CatalogUpcomingResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(tvShowId, payload.Items[0].ContentId);
        Assert.False(payload.Items[0].IsFollowed);
        Assert.Equal(movieId, payload.Items[1].ContentId);
        Assert.True(payload.Items[1].IsFollowed);
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
}
