using MovieApp.Application.Models.Credits;
using MovieApp.Contracts.Credits;

namespace MovieApp.Api.Mapping;

public static class CreditsContractMapper
{
    public static CreditsResponse ToResponse(CreditsResult result) =>
        new(
            result.Cast.Select(ToCastMemberResponse).ToList(),
            result.Crew.Select(ToCrewMemberResponse).ToList());

    private static CastMemberResponse ToCastMemberResponse(CastMemberResult member) =>
        new(
            member.ProviderPersonId,
            member.Name,
            member.Character,
            member.ProfileImagePath,
            member.Order,
            member.TotalEpisodeCount,
            member.Roles?.Select(ToCastRoleResponse).ToList());

    private static CastRoleResponse ToCastRoleResponse(CastRoleResult role) =>
        new(role.Character, role.EpisodeCount);

    private static CrewMemberResponse ToCrewMemberResponse(CrewMemberResult member) =>
        new(
            member.ProviderPersonId,
            member.Name,
            member.Department,
            member.Jobs,
            member.ProfileImagePath);
}
