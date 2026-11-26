namespace MovieApp.Contracts.Catalog;

public sealed record CatalogDetailExtrasResponse(
    string? Tagline,
    IReadOnlyDictionary<string, string> Certifications,
    IReadOnlyList<string> Networks,
    IReadOnlyList<string> Creators,
    IReadOnlyList<string> ProductionCompanies,
    long? Budget,
    long? Revenue,
    CatalogLastEpisodeResponse? LastEpisode);

public sealed record CatalogLastEpisodeResponse(int SeasonNumber, int EpisodeNumber, DateOnly? AirDate);
