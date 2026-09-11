using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public sealed class CreateWatchlistService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository) : ICreateWatchlistService
{
    public async Task<WatchlistSummaryResult> CreateAsync(
        CreateWatchlistRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var validationResult = WatchlistNameValidator.Validate(request.Name);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var normalizedName = WatchlistNameNormalizer.Normalize(request.Name);
        if (await watchlistRepository.ExistsByNormalizedNameAsync(userId, normalizedName, cancellationToken: cancellationToken))
        {
            throw new ConflictException("A watchlist with this name already exists.");
        }

        var watchlist = Watchlist.Create(userId, request.Name, DateTime.UtcNow);
        await watchlistRepository.AddAsync(watchlist, cancellationToken);

        return WatchlistMapper.ToSummaryResult(watchlist, 0);
    }
}
