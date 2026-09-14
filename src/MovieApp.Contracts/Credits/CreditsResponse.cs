namespace MovieApp.Contracts.Credits;

public sealed record CreditsResponse(IReadOnlyList<CastMemberResponse> Cast);
