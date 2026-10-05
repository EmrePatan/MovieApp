using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.Catalog;
using MovieApp.UnitTests.Keywords;

namespace MovieApp.UnitTests.Catalog;

internal static class DetailKeywordOverlayTestSupport
{
    internal static IDetailKeywordOverlayService Create(
        ICatalogTitleKeywordReadRepository? keywordRepository = null,
        ICacheService? cache = null) =>
        new DetailKeywordOverlayService(
            keywordRepository ?? new NoOpCatalogTitleKeywordReadRepository(),
            cache ?? new PassthroughCacheService());
}

internal sealed class PassthroughCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class =>
        Task.FromResult<T?>(null);

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
        where T : class =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
