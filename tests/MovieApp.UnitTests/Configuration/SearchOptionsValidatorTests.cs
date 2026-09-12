using MovieApp.Application.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class SearchOptionsValidatorTests
{
    private readonly SearchOptionsValidator _validator = new();

    [Fact]
    public void ValidateAcceptsDefaultProductionValues()
    {
        var result = _validator.Validate(
            SearchOptions.SectionName,
            new SearchOptions
            {
                CacheDuration = TimeSpan.FromHours(1),
                ProviderRefreshInterval = TimeSpan.FromHours(24),
                ProviderRefreshLockDuration = TimeSpan.FromSeconds(30)
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsWhenCacheDurationIsZero()
    {
        var result = _validator.Validate(
            SearchOptions.SectionName,
            new SearchOptions { CacheDuration = TimeSpan.Zero });

        Assert.False(result.Succeeded);
        Assert.Contains("Search:CacheDuration", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFailsWhenProviderRefreshIntervalIsTooShort()
    {
        var result = _validator.Validate(
            SearchOptions.SectionName,
            new SearchOptions
            {
                CacheDuration = TimeSpan.FromHours(1),
                ProviderRefreshInterval = TimeSpan.FromMinutes(1),
                ProviderRefreshLockDuration = TimeSpan.FromSeconds(30)
            });

        Assert.False(result.Succeeded);
        Assert.Contains("Search:ProviderRefreshInterval", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFailsWhenLockDurationIsTooShort()
    {
        var result = _validator.Validate(
            SearchOptions.SectionName,
            new SearchOptions
            {
                CacheDuration = TimeSpan.FromHours(1),
                ProviderRefreshInterval = TimeSpan.FromHours(24),
                ProviderRefreshLockDuration = TimeSpan.FromSeconds(1)
            });

        Assert.False(result.Succeeded);
        Assert.Contains("Search:ProviderRefreshLockDuration", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultOptionsMatchProductionDefaults()
    {
        var options = new SearchOptions();

        Assert.Equal(TimeSpan.FromHours(1), options.CacheDuration);
        Assert.Equal(TimeSpan.FromHours(24), options.ProviderRefreshInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ProviderRefreshLockDuration);
        Assert.Equal(20, options.MaxProviderDetailFetchesPerContentType);
        Assert.Equal(4, options.MaxConcurrentProviderHttpRequests);
        Assert.Equal(TimeSpan.FromSeconds(10), options.ProviderRefreshLockRenewalInterval);
    }
}
