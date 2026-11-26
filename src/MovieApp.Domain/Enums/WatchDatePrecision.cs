namespace MovieApp.Domain.Enums;

/// <summary>
/// How much of <c>WatchedAt</c> can be trusted. The numeric values are persisted and are also
/// referenced literally by the Insights SQL: day-level statistics use <c>IN (0, 1)</c>,
/// year-level statistics use <c>&lt;&gt; 3</c>.
/// </summary>
public enum WatchDatePrecision
{
    /// <summary>Recorded at the moment of marking.</summary>
    Exact = 0,

    /// <summary>The user picked the calendar day (or the release date).</summary>
    Day = 1,

    /// <summary>Only the year is meaningful.</summary>
    Year = 2,

    /// <summary>The user does not remember; <c>WatchedAt</c> must not feed any time statistic.</summary>
    Unknown = 3,
}
