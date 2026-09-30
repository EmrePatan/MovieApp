using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Contracts.Discovery;

namespace MovieApp.Api.Mapping;

public static class DiscoveryContractMapper
{
    public static DiscoveryWatchProvidersResponse ToWatchProvidersResponse(
        string watchRegion,
        string mediaType,
        IReadOnlyList<DiscoveryWatchProviderItem> providers) =>
        new(
            watchRegion,
            mediaType,
            providers.Select(ToWatchProviderResponse).ToList());

    public static DiscoveryKeywordsResponse ToKeywordsResponse(PaginatedResult<KeywordDiscoverItem> result) =>
        new(
            result.Items.Select(item => new DiscoveryKeywordResponse(item.KeywordId, item.Name)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    private static DiscoveryWatchProviderResponse ToWatchProviderResponse(DiscoveryWatchProviderItem provider) =>
        new(
            provider.ProviderId,
            provider.Name,
            provider.LogoPath,
            provider.DisplayPriority);
}
