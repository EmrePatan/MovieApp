namespace MovieApp.Application.Models.Library;

public sealed record LibraryNextEpisodeResult(
    Guid EpisodeId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Title);
