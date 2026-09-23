namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbTvSearchResponseJson
{
    public int Page { get; set; }

    public int TotalPages { get; set; }

    public int TotalResults { get; set; }

    public List<TmdbTvSearchResultJson> Results { get; set; } = [];
}

internal sealed class TmdbTvSearchResultJson
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? OriginalName { get; set; }

    public string? Overview { get; set; }

    public string? FirstAirDate { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public string? OriginalLanguage { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public double Popularity { get; set; }
}
