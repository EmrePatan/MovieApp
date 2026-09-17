using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Watchlists;

public sealed class GetWatchlistItemsService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository,
    IWatchlistItemRepository watchlistItemRepository) : IGetWatchlistItemsService
{
    public async Task<WatchlistItemsResult> GetAsync(
        Guid watchlistId,
        SearchContentType mediaType,
        WatchlistItemsSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var validationResult = SearchPaginationValidator.Validate(page, pageSize);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        if (await watchlistRepository.GetByIdForUserAsync(userId, watchlistId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        var (items, totalCount) = await watchlistItemRepository.GetItemsAsync(
            watchlistId,
            mediaType,
            sort,
            page,
            pageSize,
            cancellationToken);

        return WatchlistMapper.ToItemsResult(items, page, pageSize, totalCount);
    }
}
