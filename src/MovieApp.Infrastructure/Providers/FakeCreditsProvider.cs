using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Credits;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeCreditsProvider : ICreditsProvider
{
    public static readonly IReadOnlyList<CastMemberResult> InterstellarCast =
    [
        new(1001, "Matthew McConaughey", "Cooper", "/fake/cooper.jpg", 0),
        new(1002, "Anne Hathaway", "Brand", "/fake/brand.jpg", 1),
        new(1003, "Jessica Chastain", "Murph", "/fake/murph.jpg", 2),
    ];

    public static readonly IReadOnlyList<CrewMemberResult> InterstellarCrew =
    [
        new(5001, "Christopher Nolan", "Directing", ["Director"], "/fake/nolan.jpg"),
        new(5002, "Hans Zimmer", "Sound", ["Original Music Composer"], "/fake/zimmer.jpg"),
        new(5003, "Emma Thomas", "Production", ["Producer"], "/fake/emma.jpg"),
    ];

    public Task<CreditsResult> GetMovieCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CreditsResult(
            tmdbId == FakeMovieDataProvider.InterstellarTmdbId ? InterstellarCast : [],
            tmdbId == FakeMovieDataProvider.InterstellarTmdbId ? InterstellarCrew : []));

    public Task<CreditsResult> GetTvShowCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CreditsResult(
            tmdbId == FakeTvShowDataProvider.BreakingBadTmdbId
                ?
                [
                    new(
                        2001,
                        "Bryan Cranston",
                        "Walter White",
                        "/fake/walter.jpg",
                        0,
                        62,
                        [
                            new CastRoleResult("Walter White", 62),
                        ]),
                    new(
                        2002,
                        "Aaron Paul",
                        "Jesse Pinkman",
                        "/fake/jesse.jpg",
                        1,
                        62,
                        [
                            new CastRoleResult("Jesse Pinkman", 62),
                        ]),
                ]
                : [],
            tmdbId == FakeTvShowDataProvider.BreakingBadTmdbId
                ?
                [
                    new(6001, "Vince Gilligan", "Production", ["Executive Producer"], "/fake/gilligan.jpg"),
                ]
                : []));
}
