using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbTrendingWeekDataProvider(TmdbApiClient apiClient) : ITrendingWeekDataProvider
{
    public async Task<IReadOnlyList<TrendingWeekProviderItem>> GetTrendingWeekAsync(
        CancellationToken cancellationToken = default)
    {
        var page = await GetTrendingWeekPageAsync(1, cancellationToken);
        return page.Items;
    }

    public async Task<TrendingWeekPage> GetTrendingWeekPageAsync(
        int page,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbTrendingWeekQueryBuilder.BuildQuery(page);
        var response = await apiClient.GetCanonicalAsync<TmdbTrendingResponseJson>(
            $"trending/all/week?{query}",
            cancellationToken);

        if (response is null)
        {
            return new TrendingWeekPage([], page, 0, 0);
        }

        var items = new List<TrendingWeekProviderItem>();

        foreach (var result in response.Results)
        {
            var item = TmdbTrendingMapper.ToProviderItem(result);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return new TrendingWeekPage(
            items,
            response.Page == 0 ? page : response.Page,
            response.TotalResults,
            response.TotalPages);
    }
}
