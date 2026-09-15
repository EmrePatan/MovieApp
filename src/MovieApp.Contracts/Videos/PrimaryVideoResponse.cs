namespace MovieApp.Contracts.Videos;

public sealed record PrimaryVideoResponse(
    string Site,
    string Type,
    string Name,
    string? Language,
    bool Official,
    string WatchUrl);
