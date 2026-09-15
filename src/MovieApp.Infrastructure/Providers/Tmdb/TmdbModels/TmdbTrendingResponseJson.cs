namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbTrendingResponseJson
{
    public int Page { get; set; }

    public List<TmdbTrendingResultJson> Results { get; set; } = [];
}

internal sealed class TmdbTrendingResultJson
{
    public int Id { get; set; }

    public string? MediaType { get; set; }

    public string? Title { get; set; }

    public string? Name { get; set; }

    public string? OriginalTitle { get; set; }

    public string? OriginalName { get; set; }

    public string? Overview { get; set; }

    public string? ReleaseDate { get; set; }

    public string? FirstAirDate { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }
}
