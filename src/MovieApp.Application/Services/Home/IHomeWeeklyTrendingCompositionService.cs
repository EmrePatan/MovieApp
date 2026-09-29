using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

public interface IHomeWeeklyTrendingCompositionService
{
    Task<HomeWeeklyTrendingCompositionResult> ComposeAsync(
        SearchContentType type,
        int heroSize,
        int trendingSize,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
