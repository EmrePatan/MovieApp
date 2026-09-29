using MovieApp.Application.Models.WatchProviders;

namespace MovieApp.UnitTests.WatchProviders;

public sealed class TitleWatchProvidersPresentationFilterTests
{
    [Fact]
    public void Apply_includes_flatrate_and_flatrate_plus_rent_providers()
    {
        var result = TitleWatchProvidersPresentationFilter.Apply(
            new WatchProvidersResult(
                "TR",
                [
                    Provider(1, [WatchProviderAvailabilityType.Flatrate]),
                    Provider(2, [WatchProviderAvailabilityType.Flatrate, WatchProviderAvailabilityType.Rent]),
                    Provider(3, [WatchProviderAvailabilityType.Rent]),
                ],
                null));

        Assert.Equal([1, 2], result.Providers.Select(provider => provider.ProviderId).ToArray());
    }

    [Theory]
    [InlineData(WatchProviderAvailabilityType.Rent)]
    [InlineData(WatchProviderAvailabilityType.Buy)]
    [InlineData(WatchProviderAvailabilityType.Free)]
    [InlineData(WatchProviderAvailabilityType.Ads)]
    public void Apply_excludes_non_subscription_only_providers(WatchProviderAvailabilityType availabilityType)
    {
        var result = TitleWatchProvidersPresentationFilter.Apply(
            new WatchProvidersResult(
                "TR",
                [Provider(1, [availabilityType])],
                null));

        Assert.Empty(result.Providers);
    }

    [Fact]
    public void Apply_normalizes_availability_types_to_flatrate_only()
    {
        var result = TitleWatchProvidersPresentationFilter.Apply(
            new WatchProvidersResult(
                "TR",
                [Provider(8, [WatchProviderAvailabilityType.Flatrate, WatchProviderAvailabilityType.Rent])],
                null));

        Assert.Single(result.Providers);
        Assert.Equal([WatchProviderAvailabilityType.Flatrate], result.Providers[0].AvailabilityTypes);
    }

    [Fact]
    public void Apply_returns_empty_list_when_no_flatrate_providers_exist()
    {
        var result = TitleWatchProvidersPresentationFilter.Apply(
            new WatchProvidersResult(
                "TR",
                [
                    Provider(1, [WatchProviderAvailabilityType.Rent]),
                    Provider(2, [WatchProviderAvailabilityType.Buy]),
                    Provider(3, [WatchProviderAvailabilityType.Free]),
                    Provider(4, [WatchProviderAvailabilityType.Ads]),
                ],
                null));

        Assert.Empty(result.Providers);
        Assert.Equal("TR", result.Region);
    }

    private static WatchProviderResult Provider(
        int providerId,
        IReadOnlyList<WatchProviderAvailabilityType> availabilityTypes) =>
        new(providerId, $"Provider {providerId}", null, providerId, availabilityTypes, null);
}
