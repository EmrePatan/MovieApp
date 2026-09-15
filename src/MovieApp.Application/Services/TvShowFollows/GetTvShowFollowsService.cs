using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.TvShowFollows;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShowFollows;

public sealed class GetTvShowFollowsService(
    ICurrentUser currentUser,
    ITvShowFollowRepository tvShowFollowRepository,
    ITvShowRepository tvShowRepository) : IGetTvShowFollowsService
{
    public async Task<TvShowFollowsListResult> GetAsync(
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

        var (follows, totalCount) = await tvShowFollowRepository.GetUserFollowsAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        var tvShowIds = follows
            .Select(follow => follow.ContentId)
            .Distinct()
            .ToList();
        var tvShows = await tvShowRepository.GetByIdsAsync(tvShowIds, cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new TvShowFollowsListResult(
            follows
                .Where(follow => tvShows.ContainsKey(follow.ContentId))
                .Select(follow =>
                {
                    var tvShow = tvShows[follow.ContentId];
                    return new TvShowFollowTvShowResult(
                        tvShow.Id,
                        tvShow.Title,
                        tvShow.PosterPath,
                        tvShow.FirstAirDate,
                        tvShow.VoteAverage,
                        follow.NotifyNewSeasons,
                        follow.NotifyNewEpisodes,
                        follow.IsBaselineEstablished,
                        follow.CreatedAt);
                })
                .ToList(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}
