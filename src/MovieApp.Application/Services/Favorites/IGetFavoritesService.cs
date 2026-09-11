using MovieApp.Application.Models.Favorites;

namespace MovieApp.Application.Services.Favorites;

public interface IGetFavoritesService
{
    Task<FavoritesResult> GetAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
