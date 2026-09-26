using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class AutocompleteSuggestionMergerTests
{
    [Fact]
    public void MergePrefersProviderOrderThenAddsLocalMatches()
    {
        var providerId = Guid.NewGuid();
        var localId = Guid.NewGuid();

        var merged = AutocompleteSuggestionMerger.Merge(
            [new SearchSuggestion(providerId, "movie", "Provider Hit", null)],
            [new SearchSuggestion(localId, "movie", "Local Hit", null)],
            10);

        Assert.Equal(2, merged.Count);
        Assert.Equal(providerId, merged[0].Id);
        Assert.Equal(localId, merged[1].Id);
    }

    [Fact]
    public void MergeKeepsLocalSuggestionsWhenProviderReturnsEmpty()
    {
        var localId = Guid.NewGuid();

        var merged = AutocompleteSuggestionMerger.Merge(
            [],
            [new SearchSuggestion(localId, "movie", "Dönersen ıslık", null)],
            10);

        Assert.Single(merged);
        Assert.Equal(localId, merged[0].Id);
    }

    [Fact]
    public void MergeDedupesById()
    {
        var sharedId = Guid.NewGuid();

        var merged = AutocompleteSuggestionMerger.Merge(
            [new SearchSuggestion(sharedId, "movie", "Provider", null)],
            [new SearchSuggestion(sharedId, "movie", "Local", null)],
            10);

        Assert.Single(merged);
        Assert.Equal("Provider", merged[0].Title);
    }
}
