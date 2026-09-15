namespace MovieApp.Application.Models.Videos;

public sealed record PrimaryVideoResult(
    string Site,
    string Type,
    string Name,
    string? Language,
    bool Official,
    string WatchUrl);
