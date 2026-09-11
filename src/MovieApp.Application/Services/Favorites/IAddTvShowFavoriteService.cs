using MovieApp.Application.Models.Favorites;

namespace MovieApp.Application.Services.Favorites;

public interface IAddTvShowFavoriteService
{
    Task<FavoriteMutationResult> AddAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
