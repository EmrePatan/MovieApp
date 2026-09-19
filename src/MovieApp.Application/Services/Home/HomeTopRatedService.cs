using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

public sealed class HomeTopRatedService(
    IDiscoveryService discoveryService,
    IGenreReadRepository genreReadRepository,
    ISearchRepository searchRepository,
    IOptions<TopRatedOptions> options) : IHomeTopRatedService
{
    private readonly TopRatedOptions _options = options.Value;

    public async Task<IReadOnlyList<SearchItem>> GetItemsAsync(
        SearchContentType type,
        int sectionSize,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (sectionSize <= 0)
        {
            return [];
        }

        var fetchSize = Math.Max(sectionSize, _options.HomeRailCandidateFetchSize);
        var discovery = await discoveryService.GetTopRatedAsync(
            new DiscoveryCriteria(type, 1, fetchSize),
            contentLocale,
            cancellationToken);

        var rankedCandidates = discovery.Items;
        if (rankedCandidates.Count == 0)
        {
            return rankedCandidates;
        }

        var genreQualifiedKeys = await searchRepository.GetContentKeysWithAnyGenreAsync(
            rankedCandidates,
            cancellationToken);

        var eligibleCandidates = rankedCandidates
            .Where(item => genreQualifiedKeys.Contains(new CatalogContentKey(item.Id, item.Type)))
            .ToList();

        if (eligibleCandidates.Count == 0)
        {
            return [];
        }

        var animationGenreId = await genreReadRepository.GetIdByNameAsync(
            _options.HomeRailAnimationGenreName,
            cancellationToken);

        if (animationGenreId is null)
        {
            return eligibleCandidates.Take(sectionSize).ToList();
        }

        var animationContentKeys = await searchRepository.GetContentKeysWithGenreAsync(
            eligibleCandidates,
            animationGenreId.Value,
            cancellationToken);

        return TopRatedDiversityGuardrail.ApplyAnimationCap(
            eligibleCandidates,
            animationContentKeys,
            _options.HomeRailMaxAnimationItems,
            sectionSize);
    }
}
