using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsTimeZoneGuardTests
{
    [Fact]
    public void RequireValidTimeZoneRejectsMissingValue()
    {
        Assert.Throws<ValidationException>(() => InsightsTimeZoneGuard.RequireValidTimeZone(null));
        Assert.Throws<ValidationException>(() => InsightsTimeZoneGuard.RequireValidTimeZone("   "));
    }

    [Fact]
    public void RequireValidTimeZoneRejectsInvalidValue()
    {
        Assert.Throws<ValidationException>(() => InsightsTimeZoneGuard.RequireValidTimeZone("Not/AZone"));
    }

    [Fact]
    public void RequireValidTimeZoneAcceptsIanaValue()
    {
        var timeZoneId = OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul";
        var timeZone = InsightsTimeZoneGuard.RequireValidTimeZone(timeZoneId);

        Assert.Equal(timeZoneId, timeZone.Id);
    }
}
