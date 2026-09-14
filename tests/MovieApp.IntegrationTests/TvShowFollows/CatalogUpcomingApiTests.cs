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
        Assert.Equal(today.AddDays(5), payload.Items[0].ReleaseDate);
        Assert.False(payload.Items[0].IsFollowed);
        Assert.Equal(movieId, payload.Items[1].ContentId);
        Assert.Equal("Movie", payload.Items[1].ContentType);
        Assert.Equal(today.AddDays(10), payload.Items[1].ReleaseDate);
        Assert.False(payload.Items[1].IsFollowed);
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
