namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbPersonJson
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Biography { get; init; }

    public string? Birthday { get; init; }

    public string? Deathday { get; init; }

    public string? PlaceOfBirth { get; init; }

    public string? ProfilePath { get; init; }

    public string? KnownForDepartment { get; init; }
}

internal sealed class TmdbCombinedCreditsResponseJson
{
    public IReadOnlyList<TmdbCombinedCreditCastJson> Cast { get; init; } = [];
}

internal sealed class TmdbCombinedCreditCastJson
{
    public int Id { get; init; }

    public string? MediaType { get; init; }

    public string? Title { get; init; }

    public string? Name { get; init; }

    public string? Character { get; init; }

    public string? PosterPath { get; init; }

    public string? ReleaseDate { get; init; }

    public string? FirstAirDate { get; init; }
}
