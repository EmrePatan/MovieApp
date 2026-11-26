using MovieApp.Application.Services.WatchHistory;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class WatchDateRulesTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Marked = new(2026, 3, 1, 20, 0, 0, DateTimeKind.Utc);

    private static UpdateWatchDatesCommand Command(WatchDateMode mode, DateOnly? date = null, int? year = null) =>
        new([Guid.NewGuid()], [], mode, date, year);

    private static (DateTime WatchedAt, WatchDatePrecision Precision)? Resolve(
        UpdateWatchDatesCommand command,
        DateOnly? releaseDate = null) =>
        WatchDateService.Resolve(command, Marked, releaseDate, Now);

    [Fact]
    public void Day_uses_noon_utc_of_the_picked_day()
    {
        var result = Resolve(Command(WatchDateMode.Day, date: new DateOnly(2019, 5, 4)));

        Assert.Equal((new DateTime(2019, 5, 4, 12, 0, 0, DateTimeKind.Utc), WatchDatePrecision.Day), result);
    }

    [Fact]
    public void Day_never_lands_in_the_future()
    {
        var result = Resolve(Command(WatchDateMode.Day, date: DateOnly.FromDateTime(Now)));

        Assert.Equal(Now, result!.Value.WatchedAt);
    }

    [Theory]
    [InlineData(2019, 2019, 7)]
    [InlineData(2026, 2026, 3)]
    public void Year_stays_inside_the_year_and_not_in_the_future(int year, int expectedYear, int expectedMonth)
    {
        var result = Resolve(Command(WatchDateMode.Year, year: year));

        Assert.Equal(WatchDatePrecision.Year, result!.Value.Precision);
        Assert.Equal(expectedYear, result.Value.WatchedAt.Year);
        Assert.Equal(expectedMonth, result.Value.WatchedAt.Month);
    }

    [Fact]
    public void Release_uses_the_release_date()
    {
        var result = Resolve(Command(WatchDateMode.Release), new DateOnly(2010, 7, 16));

        Assert.Equal(new DateTime(2010, 7, 16, 12, 0, 0, DateTimeKind.Utc), result!.Value.WatchedAt);
    }

    [Fact]
    public void Release_skips_titles_without_a_past_release_date()
    {
        Assert.Null(Resolve(Command(WatchDateMode.Release)));
        Assert.Null(Resolve(Command(WatchDateMode.Release), new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void Unknown_keeps_the_old_timestamp()
    {
        Assert.Equal((Marked, WatchDatePrecision.Unknown), Resolve(Command(WatchDateMode.Unknown)));
    }

    [Fact]
    public void Marking_again_does_not_overwrite_an_edited_date()
    {
        var watched = WatchedMovie.Create(Guid.NewGuid(), Guid.NewGuid(), Marked);
        var edited = new DateTime(2019, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        watched.SetWatchedDate(edited, WatchDatePrecision.Year, Now);

        watched.UpdateWatchedAt(Now);

        Assert.Equal(edited, watched.WatchedAt);
    }
}
