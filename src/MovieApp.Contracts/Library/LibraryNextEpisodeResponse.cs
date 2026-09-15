namespace MovieApp.Contracts.Library;

public sealed record LibraryNextEpisodeResponse(
    Guid EpisodeId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Title);
