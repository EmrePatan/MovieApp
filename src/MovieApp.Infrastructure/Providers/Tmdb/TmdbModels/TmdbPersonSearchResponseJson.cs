namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbPersonSearchResponseJson
{
    public int Page { get; set; }

    public int TotalPages { get; set; }

    public int TotalResults { get; set; }

    public List<TmdbPersonSearchResultJson> Results { get; set; } = [];
}

internal sealed class TmdbPersonSearchResultJson
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? ProfilePath { get; set; }

    public string? KnownForDepartment { get; set; }

    public double Popularity { get; set; }
}
