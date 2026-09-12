using MovieApp.Api.Mapping;
using MovieApp.Application.Models.Search;

namespace MovieApp.UnitTests.Search;

public sealed class SearchContractMapperTests
{
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
