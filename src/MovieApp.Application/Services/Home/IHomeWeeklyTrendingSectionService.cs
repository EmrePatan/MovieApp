using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

public interface IHomeWeeklyTrendingSectionService
{
    Task<IReadOnlyList<SearchItem>> GetTrendingItemsAsync(
        SearchContentType type,
        IReadOnlyList<SearchItem> heroItems,
        int heroSize,
        int trendingSize,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
