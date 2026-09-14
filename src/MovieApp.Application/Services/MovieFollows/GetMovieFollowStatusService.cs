using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.MovieFollows;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.MovieFollows;

public sealed class GetMovieFollowStatusService(
    ICurrentUser currentUser,
    ICatalogFollowRepository catalogFollowRepository) : IGetMovieFollowStatusService
{
    public async Task<MovieFollowStatusResult> GetAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var follow = await catalogFollowRepository.GetForUserAndContentAsync(
            userId,
            CatalogContentType.Movie,
            movieId,
            cancellationToken);

        return new MovieFollowStatusResult(follow is not null);
    }
}
