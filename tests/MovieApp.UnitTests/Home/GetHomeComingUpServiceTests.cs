using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Home;

public sealed class GetHomeComingUpServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetItemsAsyncReturnsEmptyWhenUserIsNotAuthenticated()
    {
        var service = CreateService(new FakeCurrentUser(false, null), new FakeCatalogFollowCatalogRepository());

        var result = await service.GetItemsAsync(5, null, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetItemsAsyncDelegatesToRepositoryForAuthenticatedUser()
    {
        var expected = new List<CatalogUpcomingItemResult>
        {
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                CatalogContentType.Tv,
                CatalogUpcomingKind.TvEpisode,
                "Show",
                "/poster.jpg",
                new DateOnly(2026, 9, 20),
                true,
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                1,
                2,
                "Next")
        };
        var repository = new FakeCatalogFollowCatalogRepository(expected);
        var service = CreateService(new FakeCurrentUser(true, UserId), repository);

        var result = await service.GetItemsAsync(5, null, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(expected, result);
        Assert.Equal(UserId, repository.LastUserId);
        Assert.Equal(5, repository.LastLimit);
        Assert.Equal("TR", repository.LastRegion);
    }

    [Fact]
    public async Task GetItemsAsync_UsesProvidedReleaseRegion_WhenSupplied()
    {
        var repository = new FakeCatalogFollowCatalogRepository([]);
        var service = CreateService(new FakeCurrentUser(true, UserId), repository);

        await service.GetItemsAsync(5, "ES", ContentLocaleResolver.SpanishSpain);

        Assert.Equal("ES", repository.LastRegion);
    }

    [Fact]
    public async Task GetItemsAsync_UsesDefaultRegion_WhenReleaseRegionMissing()
    {
        var repository = new FakeCatalogFollowCatalogRepository([]);
        var service = CreateService(
            new FakeCurrentUser(true, UserId),
            repository,
            defaultRegion: "DE");

        await service.GetItemsAsync(5, null, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("DE", repository.LastRegion);
    }

    private static GetHomeComingUpService CreateService(
        ICurrentUser currentUser,
        ICatalogFollowCatalogRepository repository,
        string defaultRegion = "TR") =>
        new(
            currentUser,
            repository,
            new PassthroughSummaryLocalizationOverlayService(),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = defaultRegion }));

    private sealed class PassthroughSummaryLocalizationOverlayService : ISummaryLocalizationOverlayService
    {
        public Task<PaginatedResult<SearchItem>> ApplyToSearchItemsAsync(
            PaginatedResult<SearchItem> canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canonical);

        public Task<IReadOnlyList<SearchSuggestion>> ApplyToSearchSuggestionsAsync(
            IReadOnlyList<SearchSuggestion> canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canonical);

        public Task<PaginatedResult<RecommendationItem>> ApplyToRecommendationItemsAsync(
            PaginatedResult<RecommendationItem> canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canonical);

        public Task<HomeResult> ApplyToHomeResultAsync(
            HomeResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canonical);

        public Task<IReadOnlyList<RecommendationSection>> ApplyToRecommendationSectionsAsync(
            IReadOnlyList<RecommendationSection> canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canonical);

        public Task<IReadOnlyList<CatalogUpcomingItemResult>> ApplyToUpcomingItemsAsync(
            IReadOnlyList<CatalogUpcomingItemResult> canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canonical);
    }

    private sealed class FakeCurrentUser(bool isAuthenticated, Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => isAuthenticated;

        public Guid? UserId => userId;
    }

    private sealed class FakeCatalogFollowCatalogRepository(
        IReadOnlyList<CatalogUpcomingItemResult>? items = null) : ICatalogFollowCatalogRepository
    {
        public Guid? LastUserId { get; private set; }

        public int LastLimit { get; private set; }

        public string? LastRegion { get; private set; }

        public Task<(IReadOnlyList<CatalogFollowItemResult> Items, int TotalCount)> GetFollowingCatalogAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetUpcomingCatalogAsync(
            Guid? userId,
            int page,
            int pageSize,
            DateOnly today,
            string region,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetFollowedUpcomingCatalogAsync(
            Guid userId,
            int page,
            int pageSize,
            DateOnly today,
            string region,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogUpcomingItemResult>> GetFollowedUpcomingForHomeAsync(
            Guid userId,
            DateOnly today,
            string region,
            int limit,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastLimit = limit;
            LastRegion = region;
            return Task.FromResult<IReadOnlyList<CatalogUpcomingItemResult>>(items ?? []);
        }
    }
}
