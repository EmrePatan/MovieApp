using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Validation;
using MovieApp.Domain.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public sealed class UpdateWatchlistService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository,
    IWatchlistItemRepository watchlistItemRepository) : IUpdateWatchlistService
{
    public async Task<WatchlistSummaryResult> UpdateAsync(
        Guid watchlistId,
        UpdateWatchlistRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var validationResult = WatchlistNameValidator.Validate(request.Name);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var watchlist = await watchlistRepository.GetTrackedByIdForUserAsync(
            userId,
            watchlistId,
            cancellationToken);

        if (watchlist is null)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        var normalizedName = WatchlistNameNormalizer.Normalize(request.Name);
        if (await watchlistRepository.ExistsByNormalizedNameAsync(
                userId,
                normalizedName,
                watchlistId,
                cancellationToken))
        {
            throw new ConflictException("A watchlist with this name already exists.");
        }

        watchlist.Rename(request.Name, DateTime.UtcNow);
        await watchlistRepository.SaveChangesAsync(cancellationToken);

        var itemCounts = await watchlistItemRepository.GetItemCountsByWatchlistIdsAsync(
            [watchlistId],
            cancellationToken);

        return WatchlistMapper.ToSummaryResult(
            watchlist,
            itemCounts.GetValueOrDefault(watchlistId));
    }
}
