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
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Application.Validation;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Library;

public sealed class LibraryService(
    ILibraryRepository libraryRepository,
    ICurrentUser currentUser,
    IContentLocalizedPosterRepository contentLocalizedPosterRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    ILogger<LibraryService> logger) : ILibraryService
{
    public async Task<PaginatedResult<LibraryItemResult>> GetLibraryAsync(
        LibraryCriteria criteria,
        string contentLocale,
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
        var localizedPageItems = await ApplyLocalizedPostersAsync(pageItems, contentLocale, cancellationToken);

        return new PaginatedResult<LibraryItemResult>(
            localizedPageItems,
            page,
            criteria.PageSize,
            totalCount,
            totalPages,
            nextCursor,
            hasNextPage);
    }

    private async Task<IReadOnlyList<LibraryItemResult>> ApplyLocalizedPostersAsync(
        List<LibraryItemResult> items,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || items.Count == 0)
        {
            return items;
        }

        var keys = items
            .Select(item => new ContentLocalizedPosterKey(
                string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase)
                    ? CatalogContentType.Tv
                    : CatalogContentType.Movie,
                item.Id))
            .ToList();

        var movieIds = items
            .Where(item => string.Equals(item.Type, "movie", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToList();
        var tvIds = items
            .Where(item => string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToList();
        var movieOriginalLanguages = await movieRepository.GetOriginalLanguagesByIdsAsync(movieIds, cancellationToken);
        var tvOriginalLanguages = await tvShowRepository.GetOriginalLanguagesByIdsAsync(tvIds, cancellationToken);

        var localizedPosters = await LocalizedPosterDisplayOverlay.LoadPosterPathsAsync(
            contentLocalizedPosterRepository,
            keys,
            contentLocale,
            cancellationToken);

        return items
            .Select(item =>
            {
                var key = new ContentLocalizedPosterKey(
                    string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase)
                        ? CatalogContentType.Tv
                        : CatalogContentType.Movie,
                    item.Id);
                return item with
                {
                    PosterUrl = LocalizedPosterDisplayOverlay.ChooseDisplayPosterUrl(
                        item.PosterUrl,
                        key,
                        localizedPosters,
                        contentLocale,
                        string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase)
                            ? tvOriginalLanguages.GetValueOrDefault(item.Id)
                            : movieOriginalLanguages.GetValueOrDefault(item.Id))
                };
            })
            .ToList();
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
