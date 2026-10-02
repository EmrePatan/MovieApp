using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface ITrendingWeekDataProvider
{
    Task<IReadOnlyList<TrendingWeekProviderItem>> GetTrendingWeekAsync(
        CancellationToken cancellationToken = default);

    Task<TrendingWeekPage> GetTrendingWeekPageAsync(
        int page,
        CancellationToken cancellationToken = default)
    {
        return LoadPageAsync(page, cancellationToken);

        async Task<TrendingWeekPage> LoadPageAsync(int requestedPage, CancellationToken ct)
        {
            if (requestedPage > 1)
            {
                return new TrendingWeekPage([], requestedPage, 0, 1);
            }

            var items = await GetTrendingWeekAsync(ct);
            return new TrendingWeekPage(
                items,
                1,
                items.Count,
                items.Count == 0 ? 0 : 1);
        }
    }
}
