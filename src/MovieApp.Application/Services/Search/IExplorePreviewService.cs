using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IExplorePreviewService
{
    Task<ExplorePreviewResult> GetPreviewAsync(
        ExplorePreviewCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
