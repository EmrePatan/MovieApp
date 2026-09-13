using MovieApp.Application.Services.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class ProfileWatchDateHelperTests
{
    [Fact]
    public void ToDistinctLocalWatchDatesUsesProvidedTimeZone()
    {
        Assert.True(ProfileWatchDateHelper.TryGetTimeZone("Europe/Istanbul", out var timeZone));

        var dates = ProfileWatchDateHelper.ToDistinctLocalWatchDates(
            [new DateTime(2026, 4, 1, 21, 30, 0, DateTimeKind.Utc)],
            timeZone);

        Assert.Equal([new DateOnly(2026, 4, 2)], dates);
    }

    [Fact]
    public void TryGetTimeZoneRejectsInvalidId()
    {
        Assert.False(ProfileWatchDateHelper.TryGetTimeZone("Not/AZone", out _));
        Assert.False(ProfileWatchDateHelper.TryGetTimeZone(null, out _));
    }
}
