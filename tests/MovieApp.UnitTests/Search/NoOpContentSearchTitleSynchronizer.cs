using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Search;

internal sealed class NoOpContentSearchTitleSynchronizer : IContentSearchTitleSynchronizer
{
    public Task SyncCatalogTitlesAsync(
        CatalogContentType contentType,
        Guid contentId,
        string title,
        string? originalTitle,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SyncFromProviderDetailAsync(
        CatalogContentType contentType,
        Guid contentId,
        string title,
        string? originalTitle,
        IReadOnlyList<ProviderSearchTitleEntry>? providerSearchTitles,
        DateTime? providerUpdatedAtUtc,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteAllForContentAsync(
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
