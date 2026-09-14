using MovieApp.Application.Models.Credits;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbCreditsMapper
{
    public static CreditsResult ToCreditsResult(TmdbCreditsResponseJson response) =>
        new(response.Cast
            .Where(member => !string.IsNullOrWhiteSpace(member.Name))
            .Select(member => new CastMemberResult(
                member.Id,
                member.Name,
                member.Character,
                member.ProfilePath,
                member.Order))
            .ToList());

    public static CreditsResult ToCreditsResult(TmdbAggregateCreditsResponseJson response) =>
        new(response.Cast
            .Where(member => !string.IsNullOrWhiteSpace(member.Name))
            .Select(member => new CastMemberResult(
                member.Id,
                member.Name,
                member.Roles.FirstOrDefault()?.Character,
                member.ProfilePath,
                member.Order))
            .ToList());
}
