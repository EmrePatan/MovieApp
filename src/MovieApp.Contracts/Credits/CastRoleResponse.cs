namespace MovieApp.Contracts.Credits;

public sealed record CastRoleResponse(
    string? Character,
    int? EpisodeCount);
