using MovieApp.Api.Mapping;
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

    [Fact]
    public void ToWatchProvidersResultMapsFreeAndAdsAvailabilityTypes()
    {
        var response = new TmdbWatchProvidersResponseJson
        {
            Results = new Dictionary<string, TmdbWatchProviderRegionJson>
            {
                ["TR"] = new TmdbWatchProviderRegionJson
                {
                    Free =
                    [
                        new TmdbWatchProviderJson
                        {
                            ProviderId = 10,
                            ProviderName = "Free Service",
                            DisplayPriority = 1,
                        }
                    ],
                    Ads =
                    [
                        new TmdbWatchProviderJson
                        {
                            ProviderId = 11,
                            ProviderName = "Ad Service",
                            DisplayPriority = 2,
                        }
                    ],
                }
            }
        };

        var result = TmdbWatchProvidersMapper.ToWatchProvidersResult(response, "TR");

        Assert.Equal(2, result.Providers.Count);
        Assert.Contains(
            result.Providers,
            provider => provider.ProviderId == 10
                && provider.AvailabilityTypes.Contains(WatchProviderAvailabilityType.Free));
        Assert.Contains(
            result.Providers,
            provider => provider.ProviderId == 11
                && provider.AvailabilityTypes.Contains(WatchProviderAvailabilityType.Ads));
    }

    [Fact]
    public void ToWatchProvidersResultReturnsOnlyRequestedRegionProviders()
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
                            ProviderId = 1,
                            ProviderName = "TR Only",
                            DisplayPriority = 1,
                        }
                    ],
                },
                ["US"] = new TmdbWatchProviderRegionJson
                {
                    Flatrate =
                    [
                        new TmdbWatchProviderJson
                        {
                            ProviderId = 2,
                            ProviderName = "US Only",
                            DisplayPriority = 1,
                        }
                    ],
                },
            }
        };

        var result = TmdbWatchProvidersMapper.ToWatchProvidersResult(response, "TR");

        Assert.Single(result.Providers);
        Assert.Equal("TR Only", result.Providers[0].Name);
    }

    [Fact]
    public void ToWatchProvidersResultMapsAllMonetizationBucketsForContract()
    {
        var response = new TmdbWatchProvidersResponseJson
        {
            Results = new Dictionary<string, TmdbWatchProviderRegionJson>
            {
                ["TR"] = new TmdbWatchProviderRegionJson
                {
                    Flatrate =
                    [
                        new TmdbWatchProviderJson { ProviderId = 1, ProviderName = "Flat", DisplayPriority = 1 }
                    ],
                    Free =
                    [
                        new TmdbWatchProviderJson { ProviderId = 2, ProviderName = "Free", DisplayPriority = 2 }
                    ],
                    Ads =
                    [
                        new TmdbWatchProviderJson { ProviderId = 3, ProviderName = "Ads", DisplayPriority = 3 }
                    ],
                    Rent =
                    [
                        new TmdbWatchProviderJson { ProviderId = 4, ProviderName = "Rent", DisplayPriority = 4 }
                    ],
                    Buy =
                    [
                        new TmdbWatchProviderJson { ProviderId = 5, ProviderName = "Buy", DisplayPriority = 5 }
                    ],
                }
            }
        };

        var result = TmdbWatchProvidersMapper.ToWatchProvidersResult(response, "TR");
        var contract = WatchProvidersContractMapper.ToResponse(result);

        Assert.Equal(
            ["flatrate", "free", "ads", "rent", "buy"],
            contract.Providers
                .OrderBy(provider => provider.ProviderId)
                .SelectMany(provider => provider.AvailabilityTypes)
                .ToList());
    }
}
