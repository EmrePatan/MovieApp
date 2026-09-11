using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Favorites;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Favorites;

public sealed class GetFavoritesService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository) : IGetFavoritesService
{
    public async Task<FavoritesResult> GetAsync(
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

        var (favorites, totalCount) = await favoriteRepository.GetUserFavoritesAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        return FavoriteMapper.ToFavoritesResult(favorites, page, pageSize, totalCount);
    }
}
