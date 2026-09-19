using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

public interface IHotThisWeekService
{
    Task<IReadOnlyList<SearchItem>> GetItemsAsync(
        SearchContentType type,
        int maxItems,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
