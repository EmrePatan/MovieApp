using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Images;

namespace MovieApp.Application.Services.Images;

public static class ImageGalleryServiceHelper
{
    internal static readonly TimeSpan ImagesCacheTtl = TimeSpan.FromHours(24);

    public static string? ResolveLanguage(string? queryLanguage, string? acceptLanguageHeader)
    {
        if (!string.IsNullOrWhiteSpace(queryLanguage))
        {
            return NormalizeLanguage(queryLanguage);
        }

        if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
        {
            return null;
        }

        var firstLanguage = acceptLanguageHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => segment.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return NormalizeLanguage(firstLanguage);
    }

    public static string ToCacheLanguageKey(string? language) =>
        NormalizeLanguage(language) ?? "default";

    public static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var normalized = language.Trim().ToLowerInvariant();
        var separatorIndex = normalized.IndexOf('-', StringComparison.Ordinal);
        if (separatorIndex > 0)
        {
            normalized = normalized[..separatorIndex];
        }

        return normalized;
    }

    public static string? FormatTmdbIncludeImageLanguage(string? language)
    {
        var normalized = NormalizeLanguage(language);
        return normalized is null ? null : $"{normalized},null";
    }

    internal static async Task<ImagesResult> GetOrLoadAsync(
        string cacheKey,
        ICacheService cacheService,
        Func<Task<ProviderImagesResult?>> fetchProvider,
        string? preferredLanguage,
        CancellationToken cancellationToken)
    {
        var cached = await cacheService.GetAsync<ImagesCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        ProviderImagesResult? providerResult;
        try
        {
            providerResult = await fetchProvider();
        }
        catch
        {
            return ImagesResult.Empty;
        }

        if (providerResult is null)
        {
            return ImagesResult.Empty;
        }

        var result = ImageGalleryOrderer.ToImagesResult(providerResult, preferredLanguage);

        await cacheService.SetAsync(
            cacheKey,
            new ImagesCacheEntry { Result = result },
            ImagesCacheTtl,
            cancellationToken);

        return result;
    }
}
