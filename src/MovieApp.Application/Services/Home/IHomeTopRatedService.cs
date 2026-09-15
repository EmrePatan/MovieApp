using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

public interface IHomeTopRatedService
{
    Task<IReadOnlyList<SearchItem>> GetItemsAsync(
        SearchContentType type,
        int sectionSize,
        CancellationToken cancellationToken = default);
}
