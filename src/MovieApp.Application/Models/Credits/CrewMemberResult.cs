namespace MovieApp.Application.Models.Credits;

public sealed record CrewMemberResult(
    int? ProviderPersonId,
    string Name,
    string? Department,
    IReadOnlyList<string> Jobs,
    string? ProfileImagePath);
