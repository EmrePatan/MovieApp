using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Library;

public interface ILibraryService
{
    Task<PaginatedResult<LibraryItemResult>> GetLibraryAsync(
        LibraryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<LibraryItemResult>> SearchLibraryAsync(
        LibrarySearchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
