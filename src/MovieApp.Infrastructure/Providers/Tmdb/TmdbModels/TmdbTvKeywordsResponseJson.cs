namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbTvKeywordsResponseJson
{
    public int Id { get; set; }

    public List<TmdbKeywordItemJson> Results { get; set; } = [];
}
