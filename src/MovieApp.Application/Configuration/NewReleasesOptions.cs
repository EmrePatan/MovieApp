namespace MovieApp.Application.Configuration;

public sealed class NewReleasesOptions
{
    public const string SectionName = "NewReleases";

    public const int DefaultMaxAgeDays = 90;

    /// <summary>
    /// Inclusive age window for local new-releases queries (home and discovery explore).
    /// Titles must have ReleaseDate &lt;= UTC today and, when this is greater than 0,
    /// ReleaseDate &gt;= today minus this many days. Zero keeps the upper bound only.
    /// </summary>
    public int MaxAgeDays { get; set; } = DefaultMaxAgeDays;
}
