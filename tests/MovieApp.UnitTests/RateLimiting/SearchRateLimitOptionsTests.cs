using MovieApp.Api.RateLimiting;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.RateLimiting;

public sealed class SearchRateLimitOptionsTests
{
    [Fact]
    public void AutocompleteDefaultsMatchKeystrokeDrivenSearchExpectations()
    {
        var options = new SearchRateLimitOptions();

        Assert.Equal(90, options.AutocompletePermitLimit);
        Assert.Equal(1, options.AutocompleteWindowMinutes);
    }

    [Fact]
    public void AutocompletePolicyNameIsStable()
    {
        Assert.Equal("search-autocomplete", SearchRateLimitPolicies.Autocomplete);
    }
}
