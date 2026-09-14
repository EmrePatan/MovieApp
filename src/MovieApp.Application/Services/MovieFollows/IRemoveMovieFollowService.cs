namespace MovieApp.Application.Services.MovieFollows;

public interface IRemoveMovieFollowService
{
    Task RemoveAsync(Guid movieId, CancellationToken cancellationToken = default);
}
