namespace MovieApp.Application.Models.Catalog;

public sealed record CatalogProviderLookup(int? TmdbId, string? OriginalLanguage = null);
