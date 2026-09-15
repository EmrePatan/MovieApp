using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface ITrendingWeekDataProvider
{
    Task<IReadOnlyList<TrendingWeekProviderItem>> GetTrendingWeekAsync(
        CancellationToken cancellationToken = default);
}
