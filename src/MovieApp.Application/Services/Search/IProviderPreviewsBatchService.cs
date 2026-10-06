using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IProviderPreviewsBatchService
{
    Task<IReadOnlyList<ProviderPreviewGroupResult>> GetPreviewsAsync(
        ProviderPreviewsBatchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
