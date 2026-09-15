using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class ProviderSearchMapperPersonTests
{
    [Fact]
    public void ToSearchItemMapsPersonFields()
    {
        var summary = new PersonProviderSummary(3001, "Keanu Reeves", "/keanu.jpg", "Acting", 85.4m);
        var id = Guid.NewGuid();

        var item = ProviderSearchMapper.ToSearchItem(summary, id);

        Assert.Equal(id, item.Id);
        Assert.Equal("person", item.Type);
        Assert.Equal("Keanu Reeves", item.Title);
        Assert.Equal("/keanu.jpg", item.PosterUrl);
        Assert.Equal(85.4m, item.VoteAverage);
        Assert.Equal(3001, item.TmdbId);
        Assert.Equal("Acting", item.KnownForDepartment);
    }

    [Fact]
    public void MergeProviderResultsLimitsPersonsOnAllPageOne()
    {
        var criteria = new SearchCriteria(
            "keanu",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var movieResult = CreateMovieResult("Keanu Movie", 5001, 8.0m);
        var tvResult = CreateTvResult("Keanu Show", 6001, 7.5m);
        var personResult = new PersonProviderSearchResult(
        [
            new(3001, "Keanu Reeves", "/keanu.jpg", "Acting", 90m),
            new(3002, "Keanu Clone One", "/clone1.jpg", "Acting", 80m),
            new(3003, "Keanu Clone Two", "/clone2.jpg", "Acting", 70m),
            new(3004, "Keanu Clone Three", "/clone3.jpg", "Acting", 60m)
        ],
            1,
            20,
            4,
            1);

        var movieIds = new Dictionary<int, Guid> { [5001] = Guid.NewGuid() };
        var tvIds = new Dictionary<int, Guid> { [6001] = Guid.NewGuid() };
        var personIds = personResult.Results.ToDictionary(
            summary => summary.TmdbId,
            _ => Guid.NewGuid());

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            movieResult,
            tvResult,
            personResult,
            movieIds,
            tvIds,
            personIds);

        Assert.Equal(3, result.Items.Count(item => item.Type == "person"));
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public void MergeProviderResultsExcludesPersonsOnAllPageTwo()
    {
        var criteria = new SearchCriteria(
            "keanu",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            2,
            20);

        var personResult = new PersonProviderSearchResult(
            [new(3001, "Keanu Reeves", "/keanu.jpg", "Acting", 90m)],
            1,
            20,
            1,
            1);

        var personIds = new Dictionary<int, Guid> { [3001] = Guid.NewGuid() };

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            null,
            null,
            personResult,
            new Dictionary<int, Guid>(),
            new Dictionary<int, Guid>(),
            personIds);

        Assert.DoesNotContain(result.Items, item => item.Type == "person");
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public void MergeProviderResultsSortsByRelevanceThenPopularity()
    {
        var criteria = new SearchCriteria(
            "christopher",
            SearchContentType.Person,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var personResult = new PersonProviderSearchResult(
        [
            new(3002, "Christopher Nolan", "/nolan.jpg", "Directing", 72m),
            new(3005, "Other Nolan", "/other.jpg", "Acting", 99m)
        ],
            1,
            20,
            2,
            1);

        var personIds = personResult.Results.ToDictionary(
            summary => summary.TmdbId,
            _ => Guid.NewGuid());

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            null,
            null,
            personResult,
            new Dictionary<int, Guid>(),
            new Dictionary<int, Guid>(),
            personIds);

        Assert.Equal("Christopher Nolan", result.Items[0].Title);
        Assert.Equal("Other Nolan", result.Items[1].Title);
    }

    private static MovieProviderSearchResult CreateMovieResult(string title, int tmdbId, decimal voteAverage) =>
        new(
            [new($"tmdb:movie:{tmdbId}", tmdbId, null, null, title, null, null, null, voteAverage, 1000)],
            1,
            20,
            1,
            1);

    private static TvShowProviderSearchResult CreateTvResult(string title, int tmdbId, decimal voteAverage) =>
        new(
            [new($"tmdb:tv:{tmdbId}", tmdbId, null, null, title, null, null, null, null, null, null, voteAverage, 500)],
            1,
            20,
            1,
            1);
}
