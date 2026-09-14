using MovieApp.Application.Models.WatchProviders;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.WatchProviders;

public sealed class TmdbWatchProvidersMapperTests
{
    [Fact]
    public void ToWatchProvidersResultMergesDuplicateProvidersAcrossAvailabilityTypes()
    {
        var response = new TmdbWatchProvidersResponseJson
        {
            Results = new Dictionary<string, TmdbWatchProviderRegionJson>
            {
                ["TR"] = new TmdbWatchProviderRegionJson
                {
                    Link = "https://www.themoviedb.org/movie/1/watch",
                    Flatrate =
                    [
                        new TmdbWatchProviderJson
                        {
                            ProviderId = 8,
                            ProviderName = "Netflix",
                            LogoPath = "/netflix.png",
                            DisplayPriority = 2
                        }
                    ],
                    Rent =
                    [
                        new TmdbWatchProviderJson
                        {
                            ProviderId = 8,
                            ProviderName = "Netflix",
                            LogoPath = "/netflix.png",
                            DisplayPriority = 1
                        }
                    ]
                }
            }
        };

        var result = TmdbWatchProvidersMapper.ToWatchProvidersResult(response, "TR");

        Assert.Single(result.Providers);
        Assert.Equal(8, result.Providers[0].ProviderId);
        Assert.Equal(1, result.Providers[0].DisplayPriority);
        Assert.Contains(WatchProviderAvailabilityType.Flatrate, result.Providers[0].AvailabilityTypes);
        Assert.Contains(WatchProviderAvailabilityType.Rent, result.Providers[0].AvailabilityTypes);
    }

    [Fact]
    public void ToWatchProvidersResultOrdersProvidersByDisplayPriority()
    {
        var response = new TmdbWatchProvidersResponseJson
        {
            Results = new Dictionary<string, TmdbWatchProviderRegionJson>
            {
                ["TR"] = new TmdbWatchProviderRegionJson
                {
                    Flatrate =
                    [
                        new TmdbWatchProviderJson
                        {
                            ProviderId = 2,
                            ProviderName = "Prime Video",
                            DisplayPriority = 5
                        },
                        new TmdbWatchProviderJson
                        {
                            ProviderId = 1,
                            ProviderName = "Netflix",
                            DisplayPriority = 1
                        }
                    ]
                }
            }
        };

        var result = TmdbWatchProvidersMapper.ToWatchProvidersResult(response, "TR");

        Assert.Equal(["Netflix", "Prime Video"], result.Providers.Select(provider => provider.Name));
    }

    [Fact]
    public void ToWatchProvidersResultReturnsEmptyWhenRegionMissing()
    {
        var response = new TmdbWatchProvidersResponseJson
        {
            Results = new Dictionary<string, TmdbWatchProviderRegionJson>()
        };

        var result = TmdbWatchProvidersMapper.ToWatchProvidersResult(response, "TR");

        Assert.Empty(result.Providers);
    }
}
