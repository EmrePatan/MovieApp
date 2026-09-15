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
        var response = await apiClient.GetAsync<TmdbTrendingResponseJson>(
            "trending/all/week?page=1",
            cancellationToken);

        if (response is null)
        {
            return [];
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

        return items;
    }
}
