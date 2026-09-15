using MovieApp.Application.Models.Credits;
using MovieApp.Application.Services.Credits;

namespace MovieApp.UnitTests.Credits;

public sealed class CreditsNormalizerTests
{
    [Fact]
    public void NormalizeCastOrdersByOrderField()
    {
        var credits = new CreditsResult(
            [
                new(2, "Second", "B", null, 2),
                new(1, "First", "A", null, 0),
                new(3, "Third", "C", null, 1),
            ],
            []);

        var result = CreditsNormalizer.Normalize(credits);

        Assert.Equal(["First", "Third", "Second"], result.Cast.Select(member => member.Name));
    }

    [Fact]
    public void NormalizeCastDedupesDuplicatePeopleKeepingLowestOrder()
    {
        var credits = new CreditsResult(
            [
                new(1, "Actor", "Primary", "/a.jpg", 1),
                new(1, "Actor", "Duplicate", "/b.jpg", 0),
            ],
            []);

        var result = CreditsNormalizer.Normalize(credits);

        Assert.Single(result.Cast);
        Assert.Equal(0, result.Cast[0].Order);
        Assert.Equal("Duplicate", result.Cast[0].Character);
    }

    [Fact]
    public void NormalizeCastPreservesMultipleTvRoles()
    {
        var credits = new CreditsResult(
            [
                new(
                    1,
                    "Actor",
                    "Lead",
                    null,
                    0,
                    12,
                    [
                        new CastRoleResult("Lead", 10),
                        new CastRoleResult("Guest", 2),
                    ]),
            ],
            []);

        var result = CreditsNormalizer.Normalize(credits);

        Assert.Equal(2, result.Cast[0].Roles!.Count);
        Assert.Contains(result.Cast[0].Roles!, role => role.Character == "Guest");
    }

    [Fact]
    public void NormalizeCrewMergesJobsPerPersonAndDepartment()
    {
        var credits = new CreditsResult(
            [],
            [
                new(1, "Nolan", "Directing", ["Director"], null),
                new(1, "Nolan", "Directing", ["Director"], null),
                new(1, "Nolan", "Directing", ["Producer"], null),
            ]);

        var result = CreditsNormalizer.Normalize(credits);

        Assert.Single(result.Crew);
        Assert.Equal(["Director", "Producer"], result.Crew[0].Jobs);
    }

    [Fact]
    public void NormalizeDoesNotDedupeAcrossCastAndCrew()
    {
        var credits = new CreditsResult(
            [new(1, "Nolan", "Cooper", null, 0)],
            [new(1, "Nolan", "Directing", ["Director"], null)]);

        var result = CreditsNormalizer.Normalize(credits);

        Assert.Single(result.Cast);
        Assert.Single(result.Crew);
    }

    [Fact]
    public void NormalizeCrewProducesDeterministicOrdering()
    {
        var credits = new CreditsResult(
            [],
            [
                new(2, "Zimmer", "Sound", ["Composer"], null),
                new(1, "Nolan", "Directing", ["Director"], null),
            ]);

        var first = CreditsNormalizer.Normalize(credits);
        var second = CreditsNormalizer.Normalize(credits);

        Assert.Equal(first.Crew.Select(member => member.Name), second.Crew.Select(member => member.Name));
        Assert.Equal("Nolan", first.Crew[0].Name);
        Assert.Equal("Zimmer", first.Crew[1].Name);
    }
}
