using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.TvShowFollows;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Validation;

public sealed class CatalogFollowValidatorTests
{
    [Fact]
    public void ValidateTvFollow_RejectsMovieReleasePreference()
    {
        var follow = CatalogFollow.CreateTvFollow(Guid.NewGuid(), Guid.NewGuid(), true, true, DateTime.UtcNow);
        follow.NotifyMovieRelease = true;

        Assert.Throws<ValidationException>(() => CatalogFollowValidator.ValidateTvFollow(follow));
    }

    [Fact]
    public void ValidateTvFollow_RequiresAtLeastOneTvPreference()
    {
        var follow = CatalogFollow.CreateTvFollow(Guid.NewGuid(), Guid.NewGuid(), false, false, DateTime.UtcNow);

        Assert.Throws<ValidationException>(() => CatalogFollowValidator.ValidateTvFollow(follow));
    }

    [Fact]
    public void ValidateMovieFollow_EnforcesMovieRules()
    {
        var follow = CatalogFollow.CreateMovieFollow(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        CatalogFollowValidator.ValidateMovieFollow(follow);
    }

    [Fact]
    public void ValidateMovieFollowEligibility_RejectsReleasedMovies()
    {
        var today = new DateOnly(2026, 9, 15);

        Assert.Throws<ValidationException>(() =>
            CatalogFollowValidator.ValidateMovieFollowEligibility(new DateOnly(2026, 9, 14), today));
    }

    [Fact]
    public void ValidateTvPreferencesUpdate_RequiresAtLeastOnePreference()
    {
        Assert.Throws<ValidationException>(() =>
            CatalogFollowValidator.ValidateTvPreferencesUpdate(new TvShowFollowPreferencesUpdate(null, null)));
    }
}
