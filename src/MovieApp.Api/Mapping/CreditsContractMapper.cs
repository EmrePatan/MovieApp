using MovieApp.Application.Models.Credits;
using MovieApp.Contracts.Credits;

namespace MovieApp.Api.Mapping;

public static class CreditsContractMapper
{
    public static CreditsResponse ToResponse(CreditsResult result) =>
        new(result.Cast.Select(ToCastMemberResponse).ToList());

    private static CastMemberResponse ToCastMemberResponse(CastMemberResult member) =>
        new(
            member.ProviderPersonId,
            member.Name,
            member.Character,
            member.ProfileImagePath,
            member.Order);
}
