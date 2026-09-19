using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Contracts.Auth;
using MovieApp.IntegrationTests.Auth;
using MovieApp.Contracts.CatalogFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.TvShowFollows;

[Collection("TvShowFollowsApi")]
public sealed class CatalogFollowedUpcomingApiTests(TvShowFollowsApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetFollowedUpcomingCatalogIncludesFollowedFutureMovieAndExcludesUnfollowedMovie()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var followedMovieId = Guid.NewGuid();
        var unfollowedMovieId = Guid.NewGuid();
        var token = await RegisterAndGetTokenAsync("followed-upcoming-movie-user");

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            context.Movies.AddRange(
                new Movie
                {
                    Id = followedMovieId,
                    Title = "Followed Future Movie",
                    ReleaseDate = today.AddDays(8),
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                },
                new Movie
                {
                    Id = unfollowedMovieId,
                    Title = "Unfollowed Future Movie",
                    ReleaseDate = today.AddDays(2),
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            var user = await context.Users.SingleAsync(user => user.UserName == "followed-upcoming-movie-user");
            context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(user.Id, followedMovieId, utcNow));
            await context.SaveChangesAsync();
        }

        var payload = await GetFollowedUpcomingAsync(token);

        Assert.Equal(1, payload.TotalCount);
        var item = Assert.Single(payload.Items);
        Assert.Equal(followedMovieId, item.ContentId);
        Assert.Equal("MovieRelease", item.UpcomingKind);
        Assert.True(item.IsFollowed);
    }

    [Fact]
    public async Task GetFollowedUpcomingCatalogIncludesFollowedEpisodeAndExcludesUnfollowedEpisode()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var followedShowId = Guid.NewGuid();
        var unfollowedShowId = Guid.NewGuid();
        var token = await RegisterAndGetTokenAsync("followed-upcoming-episode-user");

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            SeedFollowedShowWithEpisode(context, followedShowId, today.AddDays(5), utcNow);
            SeedFollowedShowWithEpisode(context, unfollowedShowId, today.AddDays(1), utcNow);

            var user = await context.Users.SingleAsync(user => user.UserName == "followed-upcoming-episode-user");
            context.CatalogFollows.Add(CatalogFollow.CreateTvFollow(user.Id, followedShowId, true, true, utcNow));
            await context.SaveChangesAsync();
        }

        var payload = await GetFollowedUpcomingAsync(token);

        Assert.Equal(1, payload.TotalCount);
        var item = Assert.Single(payload.Items);
        Assert.Equal("TvEpisode", item.UpcomingKind);
        Assert.Equal(followedShowId, item.ContentId);
    }

    [Fact]
    public async Task GetFollowedUpcomingCatalogExcludesPastFollowedMovie()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movieId = Guid.NewGuid();
        var token = await RegisterAndGetTokenAsync("followed-upcoming-past-movie-user");

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            context.Movies.Add(new Movie
            {
                Id = movieId,
                Title = "Past Followed Movie",
                ReleaseDate = today.AddDays(-1),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            var user = await context.Users.SingleAsync(user => user.UserName == "followed-upcoming-past-movie-user");
            context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(user.Id, movieId, utcNow));
            await context.SaveChangesAsync();
        }

        var payload = await GetFollowedUpcomingAsync(token);

        Assert.Empty(payload.Items);
        Assert.Equal(0, payload.TotalCount);
    }

    [Fact]
    public async Task GetFollowedUpcomingCatalogOrdersNearestFutureFirstWithDeterministicTieBreak()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sharedDate = today.AddDays(6);
        var movieA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var movieB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var token = await RegisterAndGetTokenAsync("followed-upcoming-order-user");

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
            var user = await context.Users.SingleAsync(user => user.UserName == "followed-upcoming-order-user");
            context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(user.Id, movieA, utcNow));
            context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(user.Id, movieB, utcNow));
            await context.SaveChangesAsync();
        }

        var payload = await GetFollowedUpcomingAsync(token);

        Assert.Equal(2, payload.TotalCount);
        Assert.Equal([movieA, movieB], payload.Items.Select(item => item.ContentId).ToList());
    }

    [Fact]
    public async Task GetFollowedUpcomingCatalogPaginatesWithoutDuplicatesOrGaps()
    {
        await fixture.ResetAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movieIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var token = await RegisterAndGetTokenAsync("followed-upcoming-page-user");

        await using (var context = TvShowFollowsApiFixture.CreateContext())
        {
            var utcNow = DateTime.UtcNow;
            for (var index = 0; index < movieIds.Length; index++)
            {
                context.Movies.Add(new Movie
                {
                    Id = movieIds[index],
                    Title = $"Movie {index}",
                    ReleaseDate = today.AddDays(index + 1),
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            }

            var user = await context.Users.SingleAsync(user => user.UserName == "followed-upcoming-page-user");
            foreach (var movieId in movieIds)
            {
                context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(user.Id, movieId, utcNow));
            }

            await context.SaveChangesAsync();
        }

        var page1 = await GetFollowedUpcomingAsync(token, page: 1, pageSize: 2);
        var page2 = await GetFollowedUpcomingAsync(token, page: 2, pageSize: 2);

        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(4, page1.Items.Concat(page2.Items).Select(item => item.ContentId).Distinct().Count());
    }

    [Fact]
    public async Task GetFollowedUpcomingCatalogRequiresAuthentication()
    {
        await fixture.ResetAsync();

        var response = await _client.GetAsync("/api/catalog/upcoming?scope=followed&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static void SeedFollowedShowWithEpisode(
        ApplicationDbContext context,
        Guid tvShowId,
        DateOnly airDate,
        DateTime utcNow)
    {
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = $"Show {tvShowId:N}",
            FirstAirDate = airDate.AddYears(-1),
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Episodes.Add(new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = 1,
            Name = "Next",
            AirDate = airDate,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
    }

    private async Task<CatalogUpcomingResponse> GetFollowedUpcomingAsync(
        string token,
        int page = 1,
        int pageSize = 10)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/catalog/upcoming?scope=followed&page={page}&pageSize={pageSize}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CatalogUpcomingResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private Task<string> RegisterAndGetTokenAsync(string username) =>
        AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"{username}@example.com");
}
