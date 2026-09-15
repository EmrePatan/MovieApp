namespace MovieApp.Application.Models.Videos;

public sealed record ProviderVideoResult(
    string Site,
    string Type,
    string Key,
    string Name,
    bool Official,
    string? Language,
    string? Country,
    DateTimeOffset? PublishedAt);
