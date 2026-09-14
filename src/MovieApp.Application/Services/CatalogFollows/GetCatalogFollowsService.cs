using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.CatalogFollows;

public sealed class GetCatalogFollowsService(
    ICurrentUser currentUser,
    ICatalogFollowCatalogRepository catalogFollowCatalogRepository) : IGetCatalogFollowsService
{
    public async Task<CatalogFollowsListResult> GetAsync(
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

        var (items, totalCount) = await catalogFollowCatalogRepository.GetFollowingCatalogAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new CatalogFollowsListResult(items, page, pageSize, totalCount, totalPages);
    }
}
