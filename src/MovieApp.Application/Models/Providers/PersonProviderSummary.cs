namespace MovieApp.Application.Models.Providers;

public sealed record PersonProviderSummary(
    int TmdbId,
    string Name,
    string? ProfilePath,
    string? KnownForDepartment,
    decimal Popularity);
