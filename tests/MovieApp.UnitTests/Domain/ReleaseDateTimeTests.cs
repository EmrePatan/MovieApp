using MovieApp.Domain.Notifications;

namespace MovieApp.UnitTests.Domain;

public sealed class ReleaseDateTimeTests
{
    [Fact]
    public void ToReleaseAtUtc_UsesUtcMidnightForDateOnlyAirDate()
    {
        var airDate = new DateOnly(2026, 9, 15);
        var releaseAtUtc = ReleaseDateTime.ToReleaseAtUtc(airDate);

        Assert.Equal(DateTimeKind.Utc, releaseAtUtc.Kind);
        Assert.Equal(2026, releaseAtUtc.Year);
        Assert.Equal(9, releaseAtUtc.Month);
        Assert.Equal(15, releaseAtUtc.Day);
        Assert.Equal(0, releaseAtUtc.Hour);
        Assert.Equal(0, releaseAtUtc.Minute);
        Assert.Equal(0, releaseAtUtc.Second);
    }
}
