using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

internal static class HomeSectionBuilders
{
    public static async Task<HomeSection> BuildDiscoverySectionAsync(
        HomeSectionType sectionType,
        string title,
        Task<PaginatedResult<SearchItem>> discoveryTask,
        HomeCriteria criteria,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var discovery = await discoveryTask;
        var items = DeduplicateItems(discovery.Items.Select(HomeMapper.FromSearchItem), criteria.SectionSize);

        return new HomeSection(sectionType, title, FilterByType(items, criteria.Type), 0);
    }

    public static async Task<List<HomeSection>> BuildGenreSectionsAsync(
        IDiscoveryService discoveryService,
        IReadOnlyList<string> genreNames,
        HomeCriteria criteria,
        CancellationToken cancellationToken)
    {
        var discoveryCriteria = new DiscoveryCriteria(criteria.Type, 1, criteria.SectionSize);
        var sections = new List<HomeSection>();

        foreach (var genreName in genreNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discovery = await discoveryService.GetByGenreAsync(genreName, discoveryCriteria, cancellationToken);
            var items = DeduplicateItems(discovery.Items.Select(HomeMapper.FromSearchItem), criteria.SectionSize);
            var filteredItems = FilterByType(items, criteria.Type);

            if (filteredItems.Count == 0)
            {
                continue;
            }

            sections.Add(new HomeSection(HomeSectionType.Genre, genreName, filteredItems, 0));
        }

        return sections;
    }

    public static List<HomeItem> DeduplicateItems(IEnumerable<HomeItem> items, int sectionSize)
    {
        var seen = new HashSet<Guid>();
        var result = new List<HomeItem>();

        foreach (var item in items)
        {
            if (!seen.Add(item.Id))
            {
                continue;
            }

            result.Add(item);

            if (result.Count >= sectionSize)
            {
                break;
            }
        }

        return result;
    }

    public static List<HomeItem> FilterByType(IReadOnlyList<HomeItem> items, SearchContentType type) =>
        type switch
        {
            SearchContentType.Movie => items.Where(item => item.ContentType == "movie").ToList(),
            SearchContentType.Tv => items.Where(item => item.ContentType == "tv").ToList(),
            _ => items.ToList()
        };
}
