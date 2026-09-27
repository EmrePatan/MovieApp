using MovieApp.Application.Models.Images;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Localization;

public interface IContentLocalizedPosterSynchronizer
{
    Task SyncFromProviderPostersAsync(
        CatalogContentType contentType,
        Guid contentId,
        string? canonicalPosterPath,
        IReadOnlyList<ProviderImageResult>? providerPosters,
        string? originalLanguage = null,
        CancellationToken cancellationToken = default);
}
