using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class ProviderSearchMapperRelevanceTests
{
    [Fact]
    public void MergeProviderResults_PrefersMainHarryPotterMovieOverHighRatedSpecial()
    {
        var criteria = new SearchCriteria(
            "harry potter",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var movieResult = new MovieProviderSearchResult(
        [
            new(
                "tmdb:movie:12444",
                12444,
                null,
                null,
                "Harry Potter: A History Of Magic",
                null,
                new DateOnly(2017, 10, 28),
                null,
                8.4m,
                120,
                null,
                12.5m),
            new(
                "tmdb:movie:671",
                671,
                null,
                null,
                "Harry Potter and the Philosopher's Stone",
                null,
                new DateOnly(2001, 11, 16),
                null,
                7.9m,
                28000,
                null,
                180.2m),
        ],
            1,
            20,
            2,
            1);

        var movieIds = new Dictionary<int, Guid>
        {
            [12444] = Guid.NewGuid(),
            [671] = Guid.NewGuid(),
        };

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            movieResult,
            null,
            null,
            movieIds,
            new Dictionary<int, Guid>(),
            new Dictionary<int, Guid>());

        Assert.Equal("Harry Potter and the Philosopher's Stone", result.Items[0].Title);
        Assert.Equal(671, result.Items[0].TmdbId);
    }

    [Fact]
    public void MergeProviderResults_AllSearchPrefersCatalogTitlesOverExactPersonNameMatches()
    {
        var criteria = new SearchCriteria(
            "harry potter",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var movieResult = new MovieProviderSearchResult(
        [
            new(
                "tmdb:movie:671",
                671,
                null,
                null,
                "Harry Potter and the Philosopher's Stone",
                null,
                new DateOnly(2001, 11, 16),
                null,
                7.9m,
                28000,
                null,
                180.2m),
        ],
            1,
            20,
            1,
            1);

        var personResult = new PersonProviderSearchResult(
        [
            new(3001, "Harry Potter", null, "Crew", 4.2m),
            new(3002, "Harry Potter", null, "Acting", 3.8m),
        ],
            1,
            20,
            2,
            1);

        var movieIds = new Dictionary<int, Guid> { [671] = Guid.NewGuid() };
        var personIds = new Dictionary<int, Guid>
        {
            [3001] = Guid.NewGuid(),
            [3002] = Guid.NewGuid(),
        };

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            movieResult,
            null,
            personResult,
            movieIds,
            new Dictionary<int, Guid>(),
            personIds);

        Assert.Equal("movie", result.Items[0].Type);
        Assert.Equal("Harry Potter and the Philosopher's Stone", result.Items[0].Title);
        var firstPersonIndex = result.Items
            .Select((item, index) => (item, index))
            .Where(pair => pair.item.Type == "person")
            .Select(pair => pair.index)
            .DefaultIfEmpty(-1)
            .First();
        Assert.True(
            firstPersonIndex is -1 or > 0,
            "Catalog matches should appear before person name collisions in mixed search.");
    }
}
