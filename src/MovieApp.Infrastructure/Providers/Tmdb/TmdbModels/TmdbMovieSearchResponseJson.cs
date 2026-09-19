namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbMovieSearchResponseJson
{
    public int Page { get; set; }

    public int TotalPages { get; set; }

    public int TotalResults { get; set; }

    public List<TmdbMovieSearchResultJson> Results { get; set; } = [];
}

internal sealed class TmdbMovieSearchResultJson
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? OriginalTitle { get; set; }

    public string? Overview { get; set; }

    public string? ReleaseDate { get; set; }

    public string? PosterPath { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }
}
