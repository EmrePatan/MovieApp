using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Library;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryWatchingCursorTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task WatchingNextCursorUsesWatchingSortInProgressNotProgressPercentage()
    {
        var partialId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var caughtUpId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var lastWatchedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var repository = new StubWatchingRepository(
        [
            CreateWatchingItem(partialId, progressPercentage: 10m, watchingSortInProgress: true, lastWatchedAt),
            CreateWatchingItem(caughtUpId, progressPercentage: 100m, watchingSortInProgress: false, lastWatchedAt),
        ]);

        var service = new LibraryService(
            repository,
            new AuthenticatedCurrentUser(UserId),
            NullLogger<LibraryService>.Instance);

        var criteria = new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 1);
        var page1 = await service.GetLibraryAsync(criteria);

        Assert.NotNull(page1.NextCursor);
        Assert.True(LibraryKeysetCursor.TryDecode(
            page1.NextCursor,
            UserId,
            criteria,
            out var cursor,
            out _));
        Assert.NotNull(cursor);
        Assert.True(cursor.WatchingInProgress);
        Assert.Equal(partialId, cursor.PrimaryId);
    }

    [Fact]
    public async Task WatchingCursorForCaughtUpAnchorEncodesWatchingInProgressFalse()
    {
        var caughtUpId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var lastWatchedAt = new DateTime(2025, 6, 2, 8, 0, 0, DateTimeKind.Utc);

        var repository = new StubWatchingRepository(
        [
            CreateWatchingItem(caughtUpId, progressPercentage: 100m, watchingSortInProgress: false, lastWatchedAt),
        ]);

        var service = new LibraryService(
            repository,
            new AuthenticatedCurrentUser(UserId),
            NullLogger<LibraryService>.Instance);

        var criteria = new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 1);
        var page1 = await service.GetLibraryAsync(criteria);

        Assert.Null(page1.NextCursor);
        Assert.False(page1.HasNextPage);
    }

    private static LibraryItemResult CreateWatchingItem(
        Guid id,
        decimal progressPercentage,
        bool watchingSortInProgress,
        DateTime lastWatchedAt) =>
        new(
            id,
            "tv",
            "Title",
            null,
            "/poster.jpg",
            null,
            2024,
            8.0m,
            null,
            null,
            lastWatchedAt,
            progressPercentage,
            null,
            "watching",
            watchingSortInProgress);

    private sealed class AuthenticatedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class StubWatchingRepository(IReadOnlyList<LibraryItemResult> items) : ILibraryRepository
    {
        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchingAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default)
        {
            var pageItems = request.Page == 1
                ? items.Take(request.FetchLimit).ToList()
                : items.Skip(request.PageSize).Take(request.FetchLimit).ToList();

            return Task.FromResult(((IReadOnlyList<LibraryItemResult>)pageItems, items.Count));
        }

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetLikedAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchlistAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
