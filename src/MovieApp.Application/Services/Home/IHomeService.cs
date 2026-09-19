using MovieApp.Application.Models.Home;

namespace MovieApp.Application.Services.Home;

public interface IHomeService
{
    Task<HomeResult> GetHomeAsync(
        HomeCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<HomeBrowseResult> GetHomeBrowseAsync(
        HomeCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<HomePersonalizedResult> GetHomePersonalizedAsync(
        HomeCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
