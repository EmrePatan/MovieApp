using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;
using MovieApp.Application.Services.Localization;
using MovieApp.UnitTests.Localization;

namespace MovieApp.UnitTests.Library;

public sealed class LibrarySearchCountModeTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly JsonSerializerOptions RedisJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SearchLibraryAsync_UsesSkipCountModeForCategoryFetches()
    {
        var repository = new RecordingLibraryRepository();
        var service = CreateService(repository);

        await service.SearchLibraryAsync(
            new LibrarySearchCriteria("the", SearchContentType.All, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(LibrarySearchAggregator.Categories.Count, repository.Requests.Count);
        Assert.All(repository.Requests, request =>
        {
            Assert.Equal(LibraryCountMode.Skip, request.CountMode);
            Assert.Equal(1, request.Page);
            Assert.Equal(LibrarySearchAggregator.MaxItemsFetchedPerCategory, request.PageSize);
            Assert.Null(request.AfterCursor);
        });
    }

    [Fact]
    public async Task GetLibraryAsync_UsesRequiredCountModeForNormalPagination()
    {
        var repository = new RecordingLibraryRepository();
        var service = CreateService(repository);

        await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Liked, SearchContentType.All, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(LibraryCountMode.Required, repository.RequestedCountModes.Single());
    }

    [Fact]
    public async Task SearchLibraryAsync_MergesCategoryResultsByTitlePriority()
    {
        var alphaMovieId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var repository = new RecordingLibraryRepository
        {
            CategoryItems = new Dictionary<LibraryCategory, IReadOnlyList<LibraryItemResult>>
            {
                [LibraryCategory.Watching] = [CreateItem("Zulu", "tv", LibraryCategory.Watching)],
                [LibraryCategory.Watched] = [CreateItem("Alpha Movie", "movie", LibraryCategory.Watched, alphaMovieId)],
                [LibraryCategory.Liked] = [CreateItem("Alpha Movie", "movie", LibraryCategory.Liked, alphaMovieId)],
                [LibraryCategory.Watchlist] = [CreateItem("Beta", "tv", LibraryCategory.Watchlist)],
            },
        };
        var service = CreateService(repository);

        var result = await service.SearchLibraryAsync(
            new LibrarySearchCriteria("alpha", SearchContentType.All, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Alpha Movie"], result.Items.Select(item => item.Title).ToList());
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchLibraryAsync_ReusesTheCachedPageUntilTheLibraryGenerationChanges()
    {
        var repository = new RecordingLibraryRepository
        {
            CategoryItems = new Dictionary<LibraryCategory, IReadOnlyList<LibraryItemResult>>
            {
                [LibraryCategory.Watching] = [CreateItem("Alpha One", "movie", LibraryCategory.Watching)],
                [LibraryCategory.Watched] = [],
                [LibraryCategory.Liked] = [],
                [LibraryCategory.Watchlist] = [],
            },
        };
        var cache = new DictionaryCacheService();
        var service = CreateService(repository, cache, new LibrarySearchLoadCoordinator());

        var criteria = new LibrarySearchCriteria("alpha", SearchContentType.All, 1, 24);
        var first = await service.SearchLibraryAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);
        var scansAfterFirst = repository.Requests.Count;

        repository.CategoryItems[LibraryCategory.Watching] = [CreateItem("Alpha Two", "movie", LibraryCategory.Watching)];
        var second = await service.SearchLibraryAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Alpha One"], first.Items.Select(item => item.Title).ToArray());
        Assert.Equal(["Alpha One"], second.Items.Select(item => item.Title).ToArray());
        Assert.Equal(scansAfterFirst, repository.Requests.Count);

        await new UserRecommendationCacheGeneration(cache).InvalidateForUserAsync(UserId);
        var third = await service.SearchLibraryAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Alpha Two"], third.Items.Select(item => item.Title).ToArray());
        Assert.True(repository.Requests.Count > scansAfterFirst);
    }

    [Fact]
    public async Task SearchLibraryAsync_ConcurrentMissesShareOneCategoryScan()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingLibraryRepository
        {
            SearchEntered = entered,
            SearchRelease = release.Task,
        };
        var service = CreateService(repository, new DictionaryCacheService(), new LibrarySearchLoadCoordinator());
        var criteria = new LibrarySearchCriteria("alpha", SearchContentType.All, 1, 24);

        var first = service.SearchLibraryAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.SearchLibraryAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);
        release.TrySetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(LibrarySearchAggregator.Categories.Count, repository.Requests.Count);
        Assert.Equal(results[0].TotalCount, results[1].TotalCount);
    }

    [Fact]
    public void LibrarySearchCacheEntryRoundTripsThroughTheRedisJsonSettings()
    {
        var addedAt = new DateTime(2026, 3, 1, 8, 30, 0, DateTimeKind.Utc);
        var original = new LibrarySearchCacheEntry
        {
            Result = new PaginatedResult<LibraryItemResult>(
                [CreateItem("Alpha One", "movie", LibraryCategory.Watched) with { AddedAt = addedAt }],
                1,
                24,
                1,
                1,
                "cursor",
                false),
        };

        var json = JsonSerializer.Serialize(original, RedisJsonOptions);
        var restored = JsonSerializer.Deserialize<LibrarySearchCacheEntry>(json, RedisJsonOptions);

        Assert.NotNull(restored);
        Assert.Equal(original.Result.TotalCount, restored.Result.TotalCount);
        Assert.Equal(original.Result.NextCursor, restored.Result.NextCursor);
        Assert.Equal(original.Result.HasNextPage, restored.Result.HasNextPage);
        Assert.Equal(original.Result.Items[0].Title, restored.Result.Items[0].Title);
        Assert.Equal(original.Result.Items[0].Id, restored.Result.Items[0].Id);
        Assert.Equal(addedAt, restored.Result.Items[0].AddedAt);
    }

    private static LibraryService CreateService(
        RecordingLibraryRepository repository,
        ICacheService? cacheService = null,
        LibrarySearchLoadCoordinator? searchLoadCoordinator = null) =>
        new(
            repository,
            new AuthenticatedCurrentUser(UserId),
            new EmptyContentLocalizedPosterRepository(),
            new EmptyOriginalLanguageMovieRepository(),
            new StubTvOriginalLanguageRepository(new Dictionary<Guid, string?>()),
            NullLogger<LibraryService>.Instance,
            cacheService,
            searchLoadCoordinator);

    private static LibraryItemResult CreateItem(
        string title,
        string type,
        LibraryCategory category,
        Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            type,
            title,
            null,
            "/poster.jpg",
            null,
            2024,
            8,
            null,
            null,
            DateTime.UtcNow,
            null,
            null,
            category switch
            {
                LibraryCategory.Watching => "watching",
                LibraryCategory.Watched => "watched",
                LibraryCategory.Liked => "liked",
                _ => "watchlist",
            });

    private sealed class AuthenticatedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class RecordingLibraryRepository : ILibraryRepository
    {
        public List<LibraryCountMode> RequestedCountModes =>
            Requests.Select(request => request.CountMode).ToList();

        public List<LibraryPageRequest> Requests { get; } = [];

        public TaskCompletionSource? SearchEntered { get; init; }

        public Task? SearchRelease { get; init; }

        public Dictionary<LibraryCategory, IReadOnlyList<LibraryItemResult>> CategoryItems { get; init; } =
            new()
            {
                [LibraryCategory.Watching] = [],
                [LibraryCategory.Watched] = [],
                [LibraryCategory.Liked] = [],
                [LibraryCategory.Watchlist] = [],
            };

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchingAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watching, request);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watched, request);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetLikedAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Liked, request);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchlistAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watchlist, request);

        private async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> RecordAndReturn(
            LibraryCategory category,
            LibraryPageRequest request)
        {
            Requests.Add(request);
            if (category == LibraryCategory.Watching)
            {
                SearchEntered?.TrySetResult();
                if (SearchRelease is not null)
                {
                    await SearchRelease;
                }
            }
            var items = CategoryItems.GetValueOrDefault(category) ?? [];
            if (!request.TitleMatch.IsEmpty)
            {
                items = items
                    .Where(item => item.Title.Contains(request.TitleMatch.Primary, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return ((IReadOnlyList<LibraryItemResult>)items, items.Count);
        }
    }

    private sealed class DictionaryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
