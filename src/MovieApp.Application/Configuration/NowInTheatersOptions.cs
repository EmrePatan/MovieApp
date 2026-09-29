namespace MovieApp.Application.Configuration;

public sealed class NowInTheatersOptions
{
    public const string SectionName = "NowInTheaters";

    public const int DefaultMaxAgeDays = 60;

    /// <summary>
    /// After TMDB now_playing, keep movies whose release date is &lt;= UTC today and,
    /// when this is greater than 0, within this many days. Zero keeps the upper bound only.
    /// </summary>
    public int MaxAgeDays { get; set; } = DefaultMaxAgeDays;
}
