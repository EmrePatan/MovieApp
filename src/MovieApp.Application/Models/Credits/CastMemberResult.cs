namespace MovieApp.Application.Models.Credits;

public sealed record CastMemberResult(
    int? ProviderPersonId,
    string Name,
    string? Character,
    string? ProfileImagePath,
    int Order,
    int? TotalEpisodeCount = null,
    IReadOnlyList<CastRoleResult>? Roles = null);
