using MovieApp.Application.Models.Library;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryValidatorTests
{
    [Theory]
    [InlineData("watching", LibraryCategory.Watching)]
    [InlineData("watched", LibraryCategory.Watched)]
    [InlineData("liked", LibraryCategory.Liked)]
    [InlineData("watchlist", LibraryCategory.Watchlist)]
    public void TryParseCategoryAcceptsSupportedValues(string raw, LibraryCategory expected)
    {
        var parsed = LibraryValidator.TryParseCategory(raw, out var category);

        Assert.True(parsed);
        Assert.Equal(expected, category);
    }

    [Fact]
    public void ValidateRejectsOversizedPageSize()
    {
        var result = LibraryValidator.Validate(
            new LibraryCriteria(
                LibraryCategory.Watching,
                Application.Models.Search.SearchContentType.All,
                1,
                LibraryValidator.MaxPageSize + 1));

        Assert.False(result.IsValid);
    }
}
