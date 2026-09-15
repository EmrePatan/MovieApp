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
        CancellationToken cancellationToken = default)
    {
        if (sectionSize <= 0)
        {
            return [];
        }

        var fetchSize = Math.Max(sectionSize, _options.HomeRailCandidateFetchSize);
        var discovery = await discoveryService.GetTopRatedAsync(
            new DiscoveryCriteria(type, 1, fetchSize),
            cancellationToken);

        var rankedCandidates = discovery.Items;
        if (rankedCandidates.Count == 0)
        {
            return rankedCandidates;
        }

        var animationGenreId = await genreReadRepository.GetIdByNameAsync(
            _options.HomeRailAnimationGenreName,
            cancellationToken);

        if (animationGenreId is null)
        {
            return rankedCandidates.Take(sectionSize).ToList();
        }

        var animationContentKeys = await searchRepository.GetContentKeysWithGenreAsync(
            rankedCandidates,
            animationGenreId.Value,
            cancellationToken);

        return TopRatedDiversityGuardrail.ApplyAnimationCap(
            rankedCandidates,
            animationContentKeys,
            _options.HomeRailMaxAnimationItems,
            sectionSize);
    }
}
