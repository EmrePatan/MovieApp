using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class GenreCoverCandidatesBatchServiceTests
{
    [Fact]
    public async Task GetCandidatesAsync_ReturnsGroupsInRequestOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var browse = new FakeDiscoverBrowseService();
        browse.Results[first] = [CreateItem("movie-a")];
        browse.Results[second] = [CreateItem("movie-b")];

        var service = CreateService(browse);
        var criteria = new GenreCoverCandidatesBatchCriteria(
            [first, second],
            SearchContentType.All,
            5);

        var groups = await service.GetCandidatesAsync(
            criteria,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal([first, second], groups.Select(group => group.GenreId).ToList());
        Assert.Equal(DiscoveryBatchItemStatus.Ok, groups[0].Status);
        Assert.Equal("movie-a", groups[0].Candidates[0].Title);
        Assert.Equal("movie-b", groups[1].Candidates[0].Title);
    }

    [Fact]
    public async Task GetCandidatesAsync_PartialProviderFailureMarksTransientErrorStatus()
    {
        var successId = Guid.NewGuid();
        var failingId = Guid.NewGuid();
        var browse = new FakeDiscoverBrowseService
        {
            Results = { [successId] = [CreateItem("ok")] },
            UnavailableGenreIds = { failingId },
        };

        var service = CreateService(browse);
        var groups = await service.GetCandidatesAsync(
            new GenreCoverCandidatesBatchCriteria([successId, failingId], SearchContentType.All, 5),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(DiscoveryBatchItemStatus.Ok, groups[0].Status);
        Assert.Single(groups[0].Candidates);
        Assert.Equal(DiscoveryBatchItemStatus.TransientError, groups[1].Status);
        Assert.Empty(groups[1].Candidates);
    }

    [Fact]
    public async Task GetCandidatesAsync_PropagatesUnexpectedExceptions()
    {
        var faultingId = Guid.NewGuid();
        var browse = new FakeDiscoverBrowseService
        {
            FaultingGenreIds = { faultingId },
        };

        var service = CreateService(browse);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetCandidatesAsync(
                new GenreCoverCandidatesBatchCriteria([faultingId], SearchContentType.All, 5),
                ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public async Task GetCandidatesAsync_ThrowsWhenEveryGroupHitsProviderUnavailable()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var browse = new FakeDiscoverBrowseService
        {
            UnavailableGenreIds = { first, second },
        };

        var service = CreateService(browse);

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.GetCandidatesAsync(
                new GenreCoverCandidatesBatchCriteria([first, second], SearchContentType.All, 5),
                ContentLocaleResolver.EnglishUnitedStates));
    }

    private static GenreCoverCandidatesBatchService CreateService(FakeDiscoverBrowseService browse)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDiscoverBrowseService>(browse);
        var provider = services.BuildServiceProvider();

        return new GenreCoverCandidatesBatchService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<GenreCoverCandidatesBatchService>.Instance);
    }

    private static SearchItem CreateItem(string title) =>
        new(
            Guid.NewGuid(),
            "movie",
            title,
            title,
            null,
            null,
            null,
            null,
            0,
            0,
            2024,
            1);

    private sealed class FakeDiscoverBrowseService : IDiscoverBrowseService
    {
        public Dictionary<Guid, List<SearchItem>> Results { get; } = new();

        public HashSet<Guid> UnavailableGenreIds { get; } = [];

        public HashSet<Guid> FaultingGenreIds { get; } = [];

        public Task<PaginatedResult<SearchItem>> BrowseAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            var genreId = criteria.GenreIds.Single();
            if (FaultingGenreIds.Contains(genreId))
            {
                throw new InvalidOperationException("Simulated programming error.");
            }

            if (UnavailableGenreIds.Contains(genreId))
            {
                throw new SearchProviderUnavailableException();
            }

            var items = Results.TryGetValue(genreId, out var value)
                ? value
                : [];

            return Task.FromResult(new PaginatedResult<SearchItem>(items, 1, criteria.PageSize, items.Count, 1));
        }
    }
}
