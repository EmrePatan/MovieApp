namespace MovieApp.Application.Services.Favorites;

public interface IRemoveMovieFavoriteService
{
    Task RemoveAsync(Guid movieId, CancellationToken cancellationToken = default);
}
