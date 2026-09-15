namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbMovieKeywordsResponseJson
{
    public int Id { get; set; }

    public List<TmdbKeywordItemJson> Keywords { get; set; } = [];
}
