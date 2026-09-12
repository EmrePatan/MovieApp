using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

namespace MovieApp.UnitTests.Providers;

public sealed class TmdbTvExternalIdResolverTests
{
    [Fact]
    public void ResolveReturnsTmdbExternalIdWhenTmdbIdIsPresent()
    {
        var resolver = new TmdbTvExternalIdResolver();

        var externalId = resolver.Resolve(1399, null, null);

        Assert.Equal(TmdbExternalIdFormatter.ToExternalId(1399), externalId);
    }

    [Fact]
    public void ResolveReturnsNullWhenTmdbIdIsMissing()
    {
        var resolver = new TmdbTvExternalIdResolver();

        var externalId = resolver.Resolve(null, 121361, "tt0944947");

        Assert.Null(externalId);
    }
}
