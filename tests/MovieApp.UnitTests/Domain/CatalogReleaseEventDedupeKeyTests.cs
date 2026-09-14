using MovieApp.Domain.Notifications;

namespace MovieApp.UnitTests.Domain;

public sealed class CatalogReleaseEventDedupeKeyTests
{
    [Fact]
    public void ForEpisodeFormatsExpectedKey()
    {
        var tvShowId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var key = CatalogReleaseEventDedupeKey.ForEpisode(tvShowId, 2, 5);

        Assert.Equal("tv:11111111-1111-1111-1111-111111111111:episode:2:5", key);
    }

    [Fact]
    public void ForSeasonPremiereFormatsExpectedKey()
    {
        var tvShowId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var key = CatalogReleaseEventDedupeKey.ForSeasonPremiere(tvShowId, 3);

        Assert.Equal("tv:22222222-2222-2222-2222-222222222222:season:3:premiere", key);
    }
}
