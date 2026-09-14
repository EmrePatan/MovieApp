using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.TvShowFollows;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.TvShowFollows;

public sealed class GetTvShowFollowsService(
    ICurrentUser currentUser,
    ITvShowFollowRepository tvShowFollowRepository) : IGetTvShowFollowsService
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

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new TvShowFollowsListResult(
            follows
                .Select(follow => new TvShowFollowTvShowResult(
                    follow.TvShow.Id,
                    follow.TvShow.Title,
                    follow.TvShow.PosterPath,
                    follow.TvShow.FirstAirDate,
                    follow.TvShow.VoteAverage,
                    follow.NotifyNewSeasons,
                    follow.NotifyNewEpisodes,
                    follow.IsBaselineEstablished,
                    follow.CreatedAt))
                .ToList(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}
