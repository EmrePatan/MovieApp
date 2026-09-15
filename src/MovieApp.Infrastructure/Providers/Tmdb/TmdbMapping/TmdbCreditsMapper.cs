using MovieApp.Application.Models.Credits;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbCreditsMapper
{
    public static CreditsResult ToCreditsResult(TmdbCreditsResponseJson response) =>
        new(
            response.Cast
                .Where(member => !string.IsNullOrWhiteSpace(member.Name))
                .Select(member => new CastMemberResult(
                    member.Id,
                    member.Name,
                    member.Character,
                    member.ProfilePath,
                    member.Order))
                .ToList(),
            response.Crew
                .Where(member => !string.IsNullOrWhiteSpace(member.Name))
                .Select(member => new CrewMemberResult(
                    member.Id,
                    member.Name,
                    member.Department,
                    string.IsNullOrWhiteSpace(member.Job) ? [] : [member.Job],
                    member.ProfilePath))
                .ToList());

    public static CreditsResult ToCreditsResult(TmdbAggregateCreditsResponseJson response) =>
        new(
            response.Cast
                .Where(member => !string.IsNullOrWhiteSpace(member.Name))
                .Select(member => new CastMemberResult(
                    member.Id,
                    member.Name,
                    member.Roles.FirstOrDefault()?.Character,
                    member.ProfilePath,
                    member.Order,
                    member.TotalEpisodeCount > 0 ? member.TotalEpisodeCount : null,
                    member.Roles
                        .Where(role => !string.IsNullOrWhiteSpace(role.Character))
                        .Select(role => new CastRoleResult(
                            role.Character,
                            role.EpisodeCount > 0 ? role.EpisodeCount : null))
                        .ToList()))
                .ToList(),
            response.Crew
                .Where(member => !string.IsNullOrWhiteSpace(member.Name))
                .Select(member => new CrewMemberResult(
                    member.Id,
                    member.Name,
                    member.Department,
                    member.Jobs
                        .Select(job => job.Job)
                        .Where(job => !string.IsNullOrWhiteSpace(job))
                        .Select(job => job!)
                        .ToList(),
                    member.ProfilePath))
                .ToList());
}
