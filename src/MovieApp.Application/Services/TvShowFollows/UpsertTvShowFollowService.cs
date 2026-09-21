using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.TvShowFollows;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.TvShowFollows;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShowFollows;

public sealed class UpsertTvShowFollowService(
    ICurrentUser currentUser,
    ITvShowFollowRepository tvShowFollowRepository,
    ITvShowRepository tvShowRepository,
    ITvShowFollowBaselineJobEnqueuer tvShowFollowBaselineJobEnqueuer) : IUpsertTvShowFollowService
{
    private const int MaxCreateAttempts = 3;

    public async Task<(TvShowFollowMutationResult Mutation, TvShowFollowStatusResult Status)> UpsertAsync(
        Guid tvShowId,
        TvShowFollowPreferencesUpdate preferences,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var utcNow = DateTime.UtcNow;

        if (await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested TV show was not found.");
        }

        for (var attempt = 0; attempt < MaxCreateAttempts; attempt++)
        {
            var existing = await tvShowFollowRepository.GetForUserAndTvShowForUpdateAsync(
                userId,
                tvShowId,
                cancellationToken);

            if (existing is not null)
            {
                return await UpdateExistingAsync(existing, preferences, utcNow, cancellationToken);
            }

            var notifyNewSeasons = preferences.NotifyNewSeasons ?? true;
            var notifyNewEpisodes = preferences.NotifyNewEpisodes ?? true;

            var follow = CatalogFollow.CreateTvFollow(
                userId,
                tvShowId,
                notifyNewSeasons,
                notifyNewEpisodes,
                utcNow);

            var added = await tvShowFollowRepository.TryAddAsync(follow, cancellationToken);
            if (added)
            {
                return await CompleteCreateAsync(follow, utcNow, cancellationToken);
            }
        }

        var concurrentFollow = await tvShowFollowRepository.GetForUserAndTvShowForUpdateAsync(
            userId,
            tvShowId,
            cancellationToken);

        if (concurrentFollow is null)
        {
            throw new ConflictException("Unable to create TV show follow due to a concurrent update.");
        }

        return await UpdateExistingAsync(concurrentFollow, preferences, utcNow, cancellationToken);
    }

    private async Task<(TvShowFollowMutationResult, TvShowFollowStatusResult)> CompleteCreateAsync(
        CatalogFollow follow,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        follow.SetNotifyFromUtc(utcNow, utcNow);
        await tvShowFollowRepository.SaveChangesAsync(cancellationToken);

        await EnqueueBaselineEstablishmentAsync(follow, cancellationToken);

        var refreshedFollow = await GetRefreshedFollowAsync(follow.UserId, follow.TvShowId, cancellationToken);
        return (TvShowFollowMutationResult.Created, ToStatusResult(refreshedFollow));
    }

    private async Task<(TvShowFollowMutationResult, TvShowFollowStatusResult)> UpdateExistingAsync(
        CatalogFollow follow,
        TvShowFollowPreferencesUpdate preferences,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!follow.IsBaselineEstablished)
        {
            if (preferences.NotifyNewSeasons is not null || preferences.NotifyNewEpisodes is not null)
            {
                TvShowFollowValidator.ValidatePreferencesUpdate(preferences);

                follow.UpdateTvPreferences(
                    preferences.NotifyNewSeasons ?? follow.NotifyNewSeasons,
                    preferences.NotifyNewEpisodes ?? follow.NotifyNewEpisodes,
                    utcNow);
                await tvShowFollowRepository.SaveChangesAsync(cancellationToken);
            }

            if (!follow.NotifyFromUtc.HasValue)
            {
                follow.SetNotifyFromUtc(utcNow, utcNow);
                await tvShowFollowRepository.SaveChangesAsync(cancellationToken);
            }

            await EnqueueBaselineEstablishmentAsync(follow, cancellationToken);

            var refreshedFollow = await GetRefreshedFollowAsync(follow.UserId, follow.TvShowId, cancellationToken);
            return (TvShowFollowMutationResult.Updated, ToStatusResult(refreshedFollow));
        }

        TvShowFollowValidator.ValidatePreferencesUpdate(preferences);

        var notifyNewSeasons = preferences.NotifyNewSeasons ?? follow.NotifyNewSeasons;
        var notifyNewEpisodes = preferences.NotifyNewEpisodes ?? follow.NotifyNewEpisodes;

        follow.UpdateTvPreferences(notifyNewSeasons, notifyNewEpisodes, utcNow);
        await tvShowFollowRepository.SaveChangesAsync(cancellationToken);

        return (TvShowFollowMutationResult.Updated, ToStatusResult(follow));
    }

    private async Task EnqueueBaselineEstablishmentAsync(
        CatalogFollow follow,
        CancellationToken cancellationToken)
    {
        try
        {
            await tvShowFollowBaselineJobEnqueuer.EnqueueAsync(
                follow.UserId,
                follow.TvShowId,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not TvShowFollowBaselineException)
        {
            var refreshedFollow = await tvShowFollowRepository.GetForUserAndTvShowAsync(
                follow.UserId,
                follow.TvShowId,
                cancellationToken);

            if (refreshedFollow?.IsBaselineEstablished != true)
            {
                throw;
            }
        }
    }

    private async Task<CatalogFollow> GetRefreshedFollowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        return await tvShowFollowRepository.GetForUserAndTvShowAsync(userId, tvShowId, cancellationToken)
            ?? throw new NotFoundException("The requested TV show follow was not found.");
    }

    private static TvShowFollowStatusResult ToStatusResult(CatalogFollow follow) =>
        new(
            IsFollowing: true,
            NotifyNewSeasons: follow.NotifyNewSeasons,
            NotifyNewEpisodes: follow.NotifyNewEpisodes,
            BaselineEstablished: follow.IsBaselineEstablished);
}
