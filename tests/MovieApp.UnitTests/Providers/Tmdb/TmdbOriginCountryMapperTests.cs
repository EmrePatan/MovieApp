using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbOriginCountryMapperTests
{
    [Fact]
    public void ResolvePrimaryOriginCountryCode_PrefersTurkeyAmongProductionCountries()
    {
        var countries = new List<TmdbProductionCountryJson>
        {
            new() { Iso31661 = "US" },
            new() { Iso31661 = "TR" },
        };

        Assert.Equal("TR", TmdbOriginCountryMapper.ResolvePrimaryOriginCountryCode(countries));
    }

    [Fact]
    public void NormalizeCountryCode_RejectsInvalidValues()
    {
        Assert.Null(TmdbOriginCountryMapper.NormalizeCountryCode("USA"));
        Assert.Null(TmdbOriginCountryMapper.NormalizeCountryCode(""));
        Assert.Equal("US", TmdbOriginCountryMapper.NormalizeCountryCode("us"));
    }

    [Fact]
    public void ResolvePrimaryOriginCountryCode_SkipsInvalidEntries()
    {
        var code = TmdbOriginCountryMapper.ResolvePrimaryOriginCountryCode(["", "USA", "de"]);

        Assert.Equal("DE", code);
    }
}
