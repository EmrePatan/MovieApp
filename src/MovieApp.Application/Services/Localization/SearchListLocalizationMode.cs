namespace MovieApp.Application.Services.Localization;

/// <summary>
/// How a list page applies locale. Catalog titles keep the canonical overview and skip TMDB detail fetches.
/// </summary>
public enum SearchListLocalizationMode
{
    Full = 0,
    CatalogTitlesOnly = 1
}
