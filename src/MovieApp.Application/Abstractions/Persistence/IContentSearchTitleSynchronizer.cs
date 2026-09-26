using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IContentSearchTitleSynchronizer
{
    Task SyncCatalogTitlesAsync(
        CatalogContentType contentType,
        Guid contentId,
        string title,
        string? originalTitle,
        CancellationToken cancellationToken = default);

    Task SyncFromProviderDetailAsync(
        CatalogContentType contentType,
        Guid contentId,
        string title,
        string? originalTitle,
        IReadOnlyList<ProviderSearchTitleEntry>? providerSearchTitles,
        DateTime? providerUpdatedAtUtc,
        CancellationToken cancellationToken = default);

    Task DeleteAllForContentAsync(
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default);
}
