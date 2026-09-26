using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Library;

public interface ILibraryService
{
    Task<PaginatedResult<LibraryItemResult>> GetLibraryAsync(
        LibraryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
