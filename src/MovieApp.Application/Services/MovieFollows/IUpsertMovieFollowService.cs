using MovieApp.Application.Models.MovieFollows;

namespace MovieApp.Application.Services.MovieFollows;

public interface IUpsertMovieFollowService
{
    Task<(MovieFollowMutationResult Mutation, MovieFollowStatusResult Status)> UpsertAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);
}
