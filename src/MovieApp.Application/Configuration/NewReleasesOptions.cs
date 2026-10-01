namespace MovieApp.Application.Configuration;

public sealed class NewReleasesOptions
{
    public const string SectionName = "NewReleases";

    public const int DefaultMaxAgeDays = 90;

    /// <summary>
    /// Movie vote-count floor. Midpoint of a 50–100 quality band: high enough that a
    /// 10.0 from a handful of votes cannot lead the rail, low enough that a title with
    /// a real audience inside the 90-day window still qualifies. Zero disables the floor.
    /// </summary>
    public const int DefaultMinVoteCountMovie = 75;

    /// <summary>
    /// TV vote-count floor. Slightly below the movie floor because new series usually
    /// collect votes more slowly in the same window. Zero disables the floor.
    /// </summary>
    public const int DefaultMinVoteCountTv = 50;

    /// <summary>
    /// Inclusive age window for local new-releases queries (home and discovery explore).
    /// Titles must have ReleaseDate &lt;= UTC today and, when this is greater than 0,
    /// ReleaseDate &gt;= today minus this many days. Zero keeps the upper bound only.
    /// </summary>
    public int MaxAgeDays { get; set; } = DefaultMaxAgeDays;

    /// <summary>
    /// Hard vote-count floor for movies. Applied on the local catalog query and on
    /// TMDB discover when browse leaves that query for supplemental filters.
    /// </summary>
    public int MinVoteCountMovie { get; set; } = DefaultMinVoteCountMovie;

    /// <summary>
    /// Hard vote-count floor for TV shows. Applied on the same paths as
    /// <see cref="MinVoteCountMovie"/>.
    /// </summary>
    public int MinVoteCountTv { get; set; } = DefaultMinVoteCountTv;
}
