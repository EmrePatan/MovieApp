namespace MovieApp.Application.Services.Favorites;

public interface IGetFavoriteStatusService
{
    Task<bool> GetMovieStatusAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task<bool> GetTvShowStatusAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
