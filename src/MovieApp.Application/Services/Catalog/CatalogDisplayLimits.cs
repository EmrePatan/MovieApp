namespace MovieApp.Application.Services.Catalog;

public static class CatalogDisplayLimits
{
    public const int MaxDetailKeywords = 30;

    /// <summary>Try to surface at least this many themes on detail when the title has enough eligible keywords.</summary>
    public const int MinDetailKeywordsTarget = 6;

    public const int MaxListItemGenres = 3;
}
