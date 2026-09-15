using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetLibraryAsyncRoutesWatchingRequestsToRepository()
    {
        var repository = new StubLibraryRepository();
        var service = CreateService(repository);

        await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 24));

        Assert.Equal(LibraryCategory.Watching, repository.LastCategory);
        Assert.Equal(SearchContentType.Tv, repository.LastMediaType);
    }

    [Fact]
    public async Task GetLibraryAsyncRoutesWatchlistRequestsToRepository()
    {
        var repository = new StubLibraryRepository();
        var service = CreateService(repository);

        await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watchlist, SearchContentType.All, 2, 12));

        Assert.Equal(LibraryCategory.Watchlist, repository.LastCategory);
        Assert.Equal(2, repository.LastPage);
    }

    [Fact]
    public async Task GetLibraryAsyncReturnsPaginatedResults()
    {
        var repository = new StubLibraryRepository
        {
            Items =
            [
                CreateItem(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "tv", "watching")
            ],
            TotalCount = 1
        };
        var service = CreateService(repository);

        var result = await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.All, 1, 24));

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    private static LibraryService CreateService(ILibraryRepository repository) =>
        new(repository, new AuthenticatedCurrentUser(UserId));

    private static LibraryItemResult CreateItem(Guid id, string type, string status) =>
        new(
            id,
            type,
            "Title",
            null,
            "/poster.jpg",
            null,
            2024,
            8.0m,
            null,
            null,
            DateTime.UtcNow,
            50m,
            null,
            status);

    private sealed class AuthenticatedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class StubLibraryRepository : ILibraryRepository
    {
        public LibraryCategory LastCategory { get; private set; }

        public SearchContentType LastMediaType { get; private set; }

        public int LastPage { get; private set; }

        public IReadOnlyList<LibraryItemResult> Items { get; init; } = [];

        public int TotalCount { get; init; }

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchingAsync(
            Guid userId,
            SearchContentType mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watching, mediaType, page);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedAsync(
            Guid userId,
            SearchContentType mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watched, mediaType, page);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetLikedAsync(
            Guid userId,
            SearchContentType mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Liked, mediaType, page);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchlistAsync(
            Guid userId,
            SearchContentType mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watchlist, mediaType, page);

        private Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> RecordAndReturn(
            LibraryCategory category,
            SearchContentType mediaType,
            int page)
        {
            LastCategory = category;
            LastMediaType = mediaType;
            LastPage = page;
            return Task.FromResult(((IReadOnlyList<LibraryItemResult>)Items, TotalCount));
        }
    }
}
