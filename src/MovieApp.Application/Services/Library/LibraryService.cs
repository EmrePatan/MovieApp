using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Library;

public sealed class LibraryService(
    ILibraryRepository libraryRepository,
    ICurrentUser currentUser) : ILibraryService
{
    public async Task<PaginatedResult<LibraryItemResult>> GetLibraryAsync(
        LibraryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var validation = LibraryValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var (items, totalCount) = criteria.Category switch
        {
            LibraryCategory.Watching => await libraryRepository.GetWatchingAsync(
                userId,
                criteria.MediaType,
                criteria.Page,
                criteria.PageSize,
                cancellationToken),
            LibraryCategory.Watched => await libraryRepository.GetWatchedAsync(
                userId,
                criteria.MediaType,
                criteria.Page,
                criteria.PageSize,
                cancellationToken),
            LibraryCategory.Liked => await libraryRepository.GetLikedAsync(
                userId,
                criteria.MediaType,
                criteria.Page,
                criteria.PageSize,
                cancellationToken),
            LibraryCategory.Watchlist => await libraryRepository.GetWatchlistAsync(
                userId,
                criteria.MediaType,
                criteria.Page,
                criteria.PageSize,
                cancellationToken),
            _ => throw new ValidationException("Category must be one of: watching, watched, liked, watchlist.")
        };

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)criteria.PageSize);

        return new PaginatedResult<LibraryItemResult>(
            items,
            criteria.Page,
            criteria.PageSize,
            totalCount,
            totalPages);
    }
}
