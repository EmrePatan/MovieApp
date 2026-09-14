namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbTvChangesResponseJson
{
    public List<TmdbTvChangeItemJson> Results { get; set; } = [];

    public int Page { get; set; }

    public int TotalPages { get; set; }

    public int TotalResults { get; set; }
}

internal sealed class TmdbTvChangeItemJson
{
    public int Id { get; set; }
}
