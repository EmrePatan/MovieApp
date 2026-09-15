namespace MovieApp.Application.Models.Credits;

public sealed record CreditsResult(
    IReadOnlyList<CastMemberResult> Cast,
    IReadOnlyList<CrewMemberResult> Crew);
