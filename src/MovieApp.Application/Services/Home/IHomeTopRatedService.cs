using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

public interface IHomeTopRatedService
{
    Task<IReadOnlyList<SearchItem>> GetItemsAsync(
        SearchContentType type,
        int sectionSize,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
