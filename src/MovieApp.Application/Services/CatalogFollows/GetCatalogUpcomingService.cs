using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.CatalogFollows;

public sealed class GetCatalogUpcomingService(
    ICurrentUser currentUser,
    ICatalogFollowCatalogRepository catalogFollowCatalogRepository) : IGetCatalogUpcomingService
{
    public async Task<CatalogUpcomingListResult> GetAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var validationResult = SearchPaginationValidator.Validate(page, pageSize);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        Guid? userId = null;
        if (currentUser.IsAuthenticated)
        {
            userId = CurrentUserGuard.RequireUserId(currentUser);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (items, totalCount) = await catalogFollowCatalogRepository.GetUpcomingCatalogAsync(
            userId,
            page,
            pageSize,
            today,
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new CatalogUpcomingListResult(items, page, pageSize, totalCount, totalPages);
    }
}
