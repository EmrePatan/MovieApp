using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.RegionalRelease;

public sealed record RegionalEffectiveReleaseResult(
    DateOnly? EffectiveReleaseDate,
    TmdbReleaseType? EffectiveReleaseType,
    string? Certification,
    bool IsFallbackGlobal);
