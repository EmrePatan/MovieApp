namespace MovieApp.Application.Services.Favorites;

public interface IRemoveTvShowFavoriteService
{
    Task RemoveAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
