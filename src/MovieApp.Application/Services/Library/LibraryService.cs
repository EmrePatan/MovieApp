using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Library;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Library;

public sealed class LibraryService(
    ILibraryRepository libraryRepository,
    ICurrentUser currentUser,
    ILogger<LibraryService> logger) : ILibraryService
{
    public async Task<PaginatedResult<LibraryItemResult>> GetLibraryAsync(
        LibraryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var validation = LibraryValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var totalStopwatch = Stopwatch.StartNew();
        LibraryKeysetCursor? incomingCursor = null;
        var isCursorContinuation = !string.IsNullOrWhiteSpace(criteria.Cursor);
        if (isCursorContinuation
            && !LibraryKeysetCursor.TryDecode(criteria.Cursor, userId, criteria, out incomingCursor, out var cursorError))
        {
            throw new ValidationException(cursorError!);
        }

        var page = isCursorContinuation ? incomingCursor!.Page + 1 : criteria.Page;
        var useSnapshotTotal = isCursorContinuation && incomingCursor!.SnapshotTotalCount > 0;
        var executeCount = !useSnapshotTotal;

        var request = new LibraryPageRequest(
            page,
            criteria.PageSize,
            criteria.PageSize + 1,
            incomingCursor,
            executeCount);

        long countMs = 0;
        var countStopwatch = Stopwatch.StartNew();
        var (items, totalCount) = criteria.Category switch
        {
            LibraryCategory.Watching => await libraryRepository.GetWatchingAsync(
                userId,
                criteria.MediaType,
                request,
                cancellationToken),
            LibraryCategory.Watched => await libraryRepository.GetWatchedAsync(
                userId,
                criteria.MediaType,
                request,
                cancellationToken),
            LibraryCategory.Liked => await libraryRepository.GetLikedAsync(
                userId,
                criteria.MediaType,
                request,
                cancellationToken),
            LibraryCategory.Watchlist => await libraryRepository.GetWatchlistAsync(
                userId,
                criteria.MediaType,
                request,
                cancellationToken),
            _ => throw new ValidationException("Category must be one of: watching, watched, liked, watchlist.")
        };
        countMs = executeCount ? countStopwatch.ElapsedMilliseconds : 0;
        var pageFetchMs = totalStopwatch.ElapsedMilliseconds - countMs;

        var hasNextPage = items.Count > criteria.PageSize;
        var pageItems = items.Take(criteria.PageSize).ToList();

        string? nextCursor = null;
        if (hasNextPage && pageItems.Count > 0)
        {
            nextCursor = BuildNextCursor(userId, criteria, page, totalCount, pageItems[^1]);
        }

        var mode = isCursorContinuation
            ? "CursorContinuation"
            : criteria.Page > 1
                ? "Offset"
                : "CursorFirst";

        LibraryServiceLogMessages.LogLibraryPaginationPerf(
            logger,
            (int)criteria.Category,
            mode,
            executeCount,
            countMs,
            pageFetchMs,
            totalStopwatch.ElapsedMilliseconds,
            criteria.PageSize,
            pageItems.Count,
            hasNextPage);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)criteria.PageSize);

        return new PaginatedResult<LibraryItemResult>(
            pageItems,
            page,
            criteria.PageSize,
            totalCount,
            totalPages,
            nextCursor,
            hasNextPage);
    }

    private static string? BuildNextCursor(
        Guid userId,
        LibraryCriteria criteria,
        int page,
        int totalCount,
        LibraryItemResult lastItem)
    {
        return criteria.Category switch
        {
            LibraryCategory.Watching => LibraryKeysetCursor.Encode(
                LibraryKeysetCursor.CreateWatchingAnchor(
                    userId,
                    criteria,
                    page,
                    totalCount,
                    ResolveWatchingSortInProgress(lastItem),
                    lastItem.LastActivityAt ?? DateTime.MinValue,
                    lastItem.Id)),
            LibraryCategory.Liked => LibraryKeysetCursor.Encode(
                LibraryKeysetCursor.CreateDateAnchor(
                    userId,
                    criteria,
                    page,
                    totalCount,
                    lastItem.AddedAt ?? DateTime.MinValue,
                    null,
                    lastItem.Id)),
            LibraryCategory.Watchlist => LibraryKeysetCursor.Encode(
                LibraryKeysetCursor.CreateDateAnchor(
                    userId,
                    criteria,
                    page,
                    totalCount,
                    lastItem.AddedAt ?? DateTime.MinValue,
                    lastItem.Type,
                    lastItem.Id)),
            LibraryCategory.Watched => LibraryKeysetCursor.Encode(
                LibraryKeysetCursor.CreateDateAnchor(
                    userId,
                    criteria,
                    page,
                    totalCount,
                    lastItem.LastActivityAt ?? lastItem.WatchedAt ?? DateTime.MinValue,
                    criteria.MediaType == SearchContentType.All ? lastItem.Type : null,
                    lastItem.Id)),
            _ => null
        };
    }

    private static bool ResolveWatchingSortInProgress(LibraryItemResult lastItem)
    {
        if (lastItem.WatchingSortInProgress is bool watchingSortInProgress)
        {
            return watchingSortInProgress;
        }

        throw new InvalidOperationException(
            "Watching library items must include WatchingSortInProgress for keyset pagination.");
    }
}
