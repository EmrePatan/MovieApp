using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ILibraryRepository
{
    Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchingAsync(
        Guid userId,
        SearchContentType mediaType,
        LibraryPageRequest request,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedAsync(
        Guid userId,
        SearchContentType mediaType,
        LibraryPageRequest request,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetLikedAsync(
        Guid userId,
        SearchContentType mediaType,
        LibraryPageRequest request,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchlistAsync(
        Guid userId,
        SearchContentType mediaType,
        LibraryPageRequest request,
        CancellationToken cancellationToken = default);
}
