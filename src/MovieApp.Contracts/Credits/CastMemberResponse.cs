namespace MovieApp.Contracts.Credits;

public sealed record CastMemberResponse(
    int? ProviderPersonId,
    string Name,
    string? Character,
    string? ProfileImagePath,
    int Order);
