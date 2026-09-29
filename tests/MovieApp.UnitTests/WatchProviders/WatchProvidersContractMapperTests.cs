using MovieApp.Api.Mapping;
using MovieApp.Application.Models.WatchProviders;

namespace MovieApp.UnitTests.WatchProviders;

public sealed class WatchProvidersContractMapperTests
{
    [Fact]
    public void ToResponseMapsAllAvailabilityTypeStrings()
    {
        var result = new WatchProvidersResult(
            "TR",
            [
                new WatchProviderResult(1, "A", null, 1, [WatchProviderAvailabilityType.Flatrate], null),
                new WatchProviderResult(2, "B", null, 2, [WatchProviderAvailabilityType.Free], null),
                new WatchProviderResult(3, "C", null, 3, [WatchProviderAvailabilityType.Ads], null),
                new WatchProviderResult(4, "D", null, 4, [WatchProviderAvailabilityType.Rent], null),
                new WatchProviderResult(5, "E", null, 5, [WatchProviderAvailabilityType.Buy], null),
            ],
            null);

        var response = WatchProvidersContractMapper.ToResponse(result);

        Assert.Equal("flatrate", response.Providers[0].AvailabilityTypes[0]);
        Assert.Equal("free", response.Providers[1].AvailabilityTypes[0]);
        Assert.Equal("ads", response.Providers[2].AvailabilityTypes[0]);
        Assert.Equal("rent", response.Providers[3].AvailabilityTypes[0]);
        Assert.Equal("buy", response.Providers[4].AvailabilityTypes[0]);
    }
}
