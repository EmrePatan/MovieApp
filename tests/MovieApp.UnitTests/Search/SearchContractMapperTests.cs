using MovieApp.Api.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.UnitTests.Search;

public sealed class SearchContractMapperTests
{
    [Fact]
    public void ToAutocompleteResponseMapsPersonFields()
    {
        var items = new List<SearchSuggestion>
        {
            new(Guid.NewGuid(), "person", "Keanu Reeves", "/keanu.jpg", 3001, "Acting"),
        };

        var response = SearchContractMapper.ToAutocompleteResponse(items);

        Assert.Single(response.Items);
        Assert.Equal(3001, response.Items[0].TmdbId);
        Assert.Equal("Acting", response.Items[0].KnownForDepartment);
    }

    [Fact]
    public void ToSearchResponseMapsPersonFields()
    {
        var result = new PaginatedResult<SearchItem>(
        [
            new(
                Guid.NewGuid(),
                "person",
                "Keanu Reeves",
                null,
                null,
                "/keanu.jpg",
                null,
                null,
                85m,
                0,
                null,
                3001,
                "Acting")
        ],
            1,
            20,
            1,
            1);

        var response = SearchContractMapper.ToSearchResponse(result);

        Assert.Equal(3001, response.Items[0].TmdbId);
        Assert.Equal("Acting", response.Items[0].KnownForDepartment);
    }

    [Fact]
    public void ToAutocompleteResponseMapsPosterUrlWhenPresent()
    {
        var items = new List<SearchSuggestion>
        {
            new(Guid.NewGuid(), "movie", "Avatar", "/fake/avatar-poster.jpg"),
        };

        var response = SearchContractMapper.ToAutocompleteResponse(items);

        Assert.Single(response.Items);
        Assert.Equal("/fake/avatar-poster.jpg", response.Items[0].PosterUrl);
    }

    [Fact]
    public void ToAutocompleteResponseMapsNullPosterUrlWhenAbsent()
    {
        var items = new List<SearchSuggestion>
        {
            new(Guid.NewGuid(), "tv", "Avatar Show", null),
        };

        var response = SearchContractMapper.ToAutocompleteResponse(items);

        Assert.Single(response.Items);
        Assert.Null(response.Items[0].PosterUrl);
    }
}
