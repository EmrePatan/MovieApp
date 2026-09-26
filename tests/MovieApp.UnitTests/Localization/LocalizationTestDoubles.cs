using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Images;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Localization;

public sealed class EmptyContentLocalizedPosterRepository : IContentLocalizedPosterRepository
{
    public int GetPosterPathsCallCount { get; private set; }

    public Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> GetPosterPathsAsync(
        IReadOnlyList<ContentLocalizedPosterKey> keys,
        string languageKey,
        CancellationToken cancellationToken = default)
    {
        GetPosterPathsCallCount++;
        return Task.FromResult<IReadOnlyDictionary<ContentLocalizedPosterKey, string>>(
            new Dictionary<ContentLocalizedPosterKey, string>());
    }

    public Task UpsertAsync(
        CatalogContentType contentType,
        Guid contentId,
        string languageKey,
        string posterPath,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteAsync(
        CatalogContentType contentType,
        Guid contentId,
        string languageKey,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class InMemoryContentLocalizedPosterRepository : IContentLocalizedPosterRepository
{
    private readonly Dictionary<(CatalogContentType ContentType, Guid ContentId, string LanguageKey), string> _rows = new();

    public Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> GetPosterPathsAsync(
        IReadOnlyList<ContentLocalizedPosterKey> keys,
        string languageKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = languageKey.Trim().ToLowerInvariant();
        var result = new Dictionary<ContentLocalizedPosterKey, string>();
        foreach (var key in keys)
        {
            if (_rows.TryGetValue((key.ContentType, key.ContentId, normalizedLanguage), out var posterPath))
            {
                result[key] = posterPath;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<ContentLocalizedPosterKey, string>>(result);
    }

    public Task UpsertAsync(
        CatalogContentType contentType,
        Guid contentId,
        string languageKey,
        string posterPath,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        _rows[(contentType, contentId, languageKey.Trim().ToLowerInvariant())] = posterPath;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        CatalogContentType contentType,
        Guid contentId,
        string languageKey,
        CancellationToken cancellationToken = default)
    {
        _rows.Remove((contentType, contentId, languageKey.Trim().ToLowerInvariant()));
        return Task.CompletedTask;
    }
}

public sealed class NoOpContentLocalizedPosterSynchronizer : IContentLocalizedPosterSynchronizer
{
    public Task SyncFromProviderPostersAsync(
        CatalogContentType contentType,
        Guid contentId,
        string? canonicalPosterPath,
        IReadOnlyList<ProviderImageResult>? providerPosters,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
