using MovieApp.Application.Models.RegionalRelease;

namespace MovieApp.Application.Services.RegionalRelease;

public interface IRegionalEffectiveReleaseResolver
{
    RegionalEffectiveReleaseResult Resolve(
        string region,
        IReadOnlyList<RegionalMovieReleaseEntry> entries,
        DateOnly? globalReleaseDate);
}
