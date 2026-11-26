using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Application.Services.StreamingLeaving;
using MovieApp.Contracts.Discovery;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.WatchHistory;

[Collection("WatchHistoryApi")]
public sealed class LeavingSoonApiTests(WatchHistoryApiFixture fixture)
{
    private const string Path = "/api/discovery/leaving-soon";

    [Fact]
    public async Task FirstRequestStartsAFetchAndALaterOneReturnsTheStoredList()
    {
        await fixture.ResetAsync();
        var enqueued = new CapturingEnqueuer();
        await using var factory = CreateFactory(enqueued);
        var client = factory.CreateClient();
        await SeedMoviesAsync(factory, [900001, 900002, 900003, 900004, 900005]);

        var before = await client.GetFromJsonAsync<LeavingSoonResponse>($"{Path}?watchRegion=tr");
        Assert.Empty(before!.Items);
        Assert.Equal(["TR"], enqueued.Regions);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IStreamingLeavingFetchService>().FetchAsync("TR");
        }

        var after = await client.GetFromJsonAsync<LeavingSoonResponse>($"{Path}?watchRegion=TR");

        // The title that already left is not shown.
        Assert.Equal(
            [900001, 900002, 900003, 900004, 900005],
            after!.Items.Select(item => item.TmdbId!.Value));
        Assert.Equal("Netflix", Assert.Single(after.Items[0].Platforms).Name);
        // Fresh data: no second fetch is started.
        Assert.Equal(["TR"], enqueued.Regions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?watchRegion=ZZ")]
    [InlineData("?watchRegion=QQ")]
    [InlineData("?watchRegion=TR&sort=popularity_desc")]
    [InlineData("?watchRegion=TR&type=person")]
    public async Task InvalidRequestsAreRejected(string query)
    {
        var enqueued = new CapturingEnqueuer();
        await using var factory = CreateFactory(enqueued);

        var response = await factory.CreateClient().GetAsync(Path + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // A rejected request never costs quota.
        Assert.Empty(enqueued.Regions);
    }

    [Fact]
    public async Task ParallelReservationsNeverPassTheLimit()
    {
        await fixture.ResetAsync();
        await using var factory = CreateFactory(new CapturingEnqueuer());
        var period = new DateOnly(2026, 11, 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(async index =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IStreamingLeavingRepository>()
                .TryReserveRequestAsync(
                    period,
                    index % 2 == 0 ? "TR" : "US",
                    usage => usage.Values.Sum() < 10);
        }));

        Assert.Equal(10, results.Count(reserved => reserved));
    }

    private WebApplicationFactory<Program> CreateFactory(CapturingEnqueuer enqueuer) =>
        fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("StreamingAvailability:Enabled", "true");
            builder.UseSetting("StreamingAvailability:Provider", "Fake");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStreamingLeavingFetchJobEnqueuer>(enqueuer);
                services.AddScoped<IStreamingAvailabilityProvider, StubProvider>();
            });
        });

    private static async Task SeedMoviesAsync(WebApplicationFactory<Program> factory, int[] tmdbIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var tmdbId in tmdbIds.Append(900009))
        {
            dbContext.Movies.Add(new Movie
            {
                Id = Guid.NewGuid(),
                TmdbId = tmdbId,
                Title = $"Leaving {tmdbId}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed class CapturingEnqueuer : IStreamingLeavingFetchJobEnqueuer
    {
        public List<string> Regions { get; } = [];

        public void EnqueueFetch(string region) => Regions.Add(region);
    }

    private sealed class StubProvider : IStreamingAvailabilityProvider
    {
        public Task<IReadOnlyList<StreamingService>> GetSubscriptionServicesAsync(
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StreamingService>>([new StreamingService("netflix", "Netflix")]);

        public Task<StreamingLeavingPage> GetExpiringAsync(
            string region,
            IReadOnlyList<string> serviceIds,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var changes = Enumerable.Range(1, 5)
                .Select(day => Change(900000 + day, now.AddDays(day)))
                .Append(Change(900009, now.AddDays(-1)))
                .ToList();
            return Task.FromResult(new StreamingLeavingPage(changes, null));
        }

        private static StreamingLeavingChange Change(int tmdbId, DateTime leavesAt) =>
            new("netflix", new StreamingTitleKey(CatalogContentType.Movie, tmdbId), leavesAt, null);
    }
}
