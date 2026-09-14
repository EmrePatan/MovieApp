using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.TvShows;

public sealed class TvShowCatalogSyncStateRepositoryTests
{
    [Theory]
    [InlineData(null, "2026-09-14T20:00:00Z", true)]
    [InlineData("2026-09-14T20:00:00Z", "2026-09-14T20:01:00Z", true)]
    [InlineData("2026-09-14T20:01:00Z", "2026-09-14T20:00:00Z", false)]
    [InlineData("2026-09-14T20:00:00Z", "2026-09-14T20:00:00Z", true)]
    public void ShouldApplyRefresh_UsesMonotonicTimestampSemantics(
        string? existingRefreshedAtUtc,
        string incomingRefreshedAtUtc,
        bool expected)
    {
        DateTime? existing = existingRefreshedAtUtc is null
            ? null
            : DateTime.Parse(existingRefreshedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);

        var incoming = DateTime.Parse(incomingRefreshedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);

        Assert.Equal(expected, TvShowCatalogSyncStateRepository.ShouldApplyRefresh(existing, incoming));
    }

    [Theory]
    [InlineData(null, "2026-09-14", true)]
    [InlineData("2026-09-14", "2026-09-15", true)]
    [InlineData("2026-09-15", "2026-09-14", false)]
    public void ShouldApplyChangeSignal_UsesMonotonicDateSemantics(
        string? existingSignalDate,
        string incomingSignalDate,
        bool expected)
    {
        DateOnly? existing = existingSignalDate is null
            ? null
            : DateOnly.Parse(existingSignalDate, System.Globalization.CultureInfo.InvariantCulture);

        var incoming = DateOnly.Parse(incomingSignalDate, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, TvShowCatalogSyncStateRepository.ShouldApplyChangeSignal(existing, incoming));
    }
}
