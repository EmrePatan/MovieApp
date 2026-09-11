using MovieApp.Application.Models.Favorites;

namespace MovieApp.Application.Services.Favorites;

public interface IAddMovieFavoriteService
{
    Task<FavoriteMutationResult> AddAsync(Guid movieId, CancellationToken cancellationToken = default);
}
