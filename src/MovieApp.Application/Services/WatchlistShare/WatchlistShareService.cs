using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.WatchlistShare;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.WatchlistShare;

public sealed class WatchlistShareService(
    ICurrentUser currentUser,
    IUserWatchlistShareRepository shareRepository,
    IWatchlistItemRepository watchlistItemRepository,
    IOptions<CatalogShareOptions> catalogShareOptions) : IWatchlistShareService
{
    private const int PublicItemLimit = 250;

    public async Task<WatchlistShareStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var share = await shareRepository.GetByUserIdAsync(userId, cancellationToken);
        return new WatchlistShareStatusResult(share is { IsActive: true });
    }

    public async Task<WatchlistShareEnableResult> EnableAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var existing = await shareRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is { IsActive: true })
        {
            return new WatchlistShareEnableResult(string.Empty, CreatedNewLink: false);
        }

        var rawToken = PasswordResetTokenGenerator.GenerateToken();
        var tokenHash = PasswordResetTokenHasher.HashToken(rawToken);
        var utcNow = DateTime.UtcNow;

        if (existing is null)
        {
            await shareRepository.AddAsync(
                new UserWatchlistShare
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TokenHash = tokenHash,
                    IsActive = true,
                    CreatedAtUtc = utcNow,
                },
                cancellationToken);
        }
        else
        {
            existing.TokenHash = tokenHash;
            existing.IsActive = true;
            existing.DeactivatedAtUtc = null;
            existing.RotatedAtUtc = utcNow;
            await shareRepository.UpdateAsync(existing, cancellationToken);
        }

        var shareUrl = WatchlistShareUrlBuilder.BuildCanonicalShareUrl(catalogShareOptions.Value, rawToken);
        return new WatchlistShareEnableResult(shareUrl, CreatedNewLink: true);
    }

    public async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var existing = await shareRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null || !existing.IsActive)
        {
            return;
        }

        existing.IsActive = false;
        existing.DeactivatedAtUtc = DateTime.UtcNow;
        await shareRepository.UpdateAsync(existing, cancellationToken);
    }

    public async Task<WatchlistShareRotateResult> RotateAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var existing = await shareRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null || !existing.IsActive)
        {
            throw new NotFoundException("Watchlist sharing is not enabled.");
        }

        var rawToken = PasswordResetTokenGenerator.GenerateToken();
        existing.TokenHash = PasswordResetTokenHasher.HashToken(rawToken);
        existing.RotatedAtUtc = DateTime.UtcNow;
        await shareRepository.UpdateAsync(existing, cancellationToken);

        var shareUrl = WatchlistShareUrlBuilder.BuildCanonicalShareUrl(catalogShareOptions.Value, rawToken);
        return new WatchlistShareRotateResult(shareUrl);
    }

    public async Task<PublicWatchlistShareResult?> TryGetPublicByTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        if (!WatchlistShareTokenParser.IsValidPublicToken(rawToken))
        {
            return null;
        }

        var tokenHash = PasswordResetTokenHasher.HashToken(rawToken.Trim());
        var share = await shareRepository.GetActiveByTokenHashAsync(tokenHash, cancellationToken);
        if (share is null)
        {
            return null;
        }

        var items = await watchlistItemRepository.GetDistinctItemsForUserAsync(
            share.UserId,
            PublicItemLimit,
            cancellationToken);

        var mapped = items
            .Select(MapPublicItem)
            .Where(item => item is not null)
            .Cast<PublicWatchlistShareItemResult>()
            .ToList();

        var displayName = WatchlistShareDisplayName.ResolvePublicDisplayName(share.User);
        return new PublicWatchlistShareResult(displayName, mapped);
    }

    private static PublicWatchlistShareItemResult? MapPublicItem(WatchlistItem item)
    {
        if (item.Movie is not null)
        {
            return new PublicWatchlistShareItemResult(
                "movie",
                item.Movie.Id,
                item.Movie.Title,
                item.Movie.ReleaseDate?.Year,
                item.Movie.PosterPath,
                item.Movie.VoteAverage);
        }

        if (item.TvShow is not null)
        {
            return new PublicWatchlistShareItemResult(
                "tv",
                item.TvShow.Id,
                item.TvShow.Title,
                item.TvShow.FirstAirDate?.Year,
                item.TvShow.PosterPath,
                item.TvShow.VoteAverage);
        }

        return null;
    }
}
