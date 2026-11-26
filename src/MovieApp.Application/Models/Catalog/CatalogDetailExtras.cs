namespace MovieApp.Application.Models.Catalog;

/// <summary>Detail-page facts from TMDB that are only displayed, never queried.</summary>
public sealed record CatalogDetailExtras
{
    public string? Tagline { get; init; }

    /// <summary>Age rating by ISO 3166-1 country code. The client picks its own region.</summary>
    public IReadOnlyDictionary<string, string> Certifications { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<string> Networks { get; init; } = [];

    public IReadOnlyList<string> Creators { get; init; } = [];

    public IReadOnlyList<string> ProductionCompanies { get; init; } = [];

    /// <summary>US dollars; movies only.</summary>
    public long? Budget { get; init; }

    /// <summary>US dollars; movies only.</summary>
    public long? Revenue { get; init; }

    public CatalogLastEpisode? LastEpisode { get; init; }
}

public sealed record CatalogLastEpisode(int SeasonNumber, int EpisodeNumber, DateOnly? AirDate);
