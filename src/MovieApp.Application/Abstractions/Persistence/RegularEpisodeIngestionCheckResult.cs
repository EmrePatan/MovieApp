namespace MovieApp.Application.Abstractions.Persistence;

public sealed record RegularEpisodeIngestionCheckResult(
    bool IsRequired,
    long CatalogMetadataQueryMs,
    long SeasonsWithEpisodesQueryMs,
    int RegularSeasonCount,
    int SeasonsWithEpisodeRowsCount,
    int SeasonsMissingEpisodesCount,
    IReadOnlyList<int> MissingSeasonNumbers);
