namespace MovieApp.Contracts.Credits;

public sealed record CrewMemberResponse(
    int? ProviderPersonId,
    string Name,
    string? Department,
    IReadOnlyList<string> Jobs,
    string? ProfileImagePath);
