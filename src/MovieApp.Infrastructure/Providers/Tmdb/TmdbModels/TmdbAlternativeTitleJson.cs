namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbAlternativeTitleJson
{
    public string? Iso31661 { get; set; }

    public string? Title { get; set; }

    public string? Type { get; set; }
}

internal sealed class TmdbMovieAlternativeTitlesAppendJson
{
    public List<TmdbAlternativeTitleJson> Titles { get; set; } = [];
}

internal sealed class TmdbTvAlternativeTitlesAppendJson
{
    public List<TmdbAlternativeTitleJson> Results { get; set; } = [];
}
