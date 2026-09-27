using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Images;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
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
        string? originalLanguage = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class EmptyOriginalLanguageMovieRepository : IMovieRepository
{
    public Task<IReadOnlyDictionary<Guid, string?>> GetOriginalLanguagesByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string?>>(new Dictionary<Guid, string?>());

    public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Movie> UpsertFromProviderAsync(
        MovieProviderDetails details,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

public sealed class StubTvOriginalLanguageRepository(IReadOnlyDictionary<Guid, string?> languages) : ITvShowRepository
{
    public Task<IReadOnlyDictionary<Guid, string?>> GetOriginalLanguagesByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, string?>();
        foreach (var id in ids)
        {
            if (languages.TryGetValue(id, out var language))
            {
                result[id] = language;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, string?>>(result);
    }

    public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TvShow> UpsertFromProviderAsync(
        TvShowProviderDetails details,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
