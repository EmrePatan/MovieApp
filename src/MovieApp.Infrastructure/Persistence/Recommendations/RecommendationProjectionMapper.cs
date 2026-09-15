using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Infrastructure.Persistence.Recommendations;

internal static class RecommendationProjectionMapper
{
    internal static SimilaritySourceProfile ToSimilaritySourceProfile(RecommendationCandidateProjection projection) =>
        new(
            projection.Id,
            projection.Type,
            projection.Title,
            projection.GenreIds,
            ToGenreDictionary(projection.GenreIds, projection.GenreNames),
            projection.PersonIds,
            projection.VoteAverage,
            projection.Year);

    internal static SimilarityCandidateProfile ToSimilarityCandidateProfile(RecommendationCandidateProjection projection) =>
        new(
            projection.Id,
            projection.Type,
            projection.Title,
            projection.OriginalTitle,
            projection.Overview,
            projection.PosterUrl,
            projection.BackdropUrl,
            projection.ReleaseDate,
            projection.VoteAverage,
            projection.VoteCount,
            projection.Year,
            projection.GenreIds,
            ToGenreDictionary(projection.GenreIds, projection.GenreNames),
            projection.PersonIds);

    internal static PersonalizedCandidateProfile ToPersonalizedCandidateProfile(
        RecommendationCandidateProjection projection,
        IReadOnlyList<Guid>? keywordIds = null)
    {
        var profile = new PersonalizedCandidateProfile(
            projection.Id,
            projection.Type,
            projection.Title,
            projection.OriginalTitle,
            projection.Overview,
            projection.PosterUrl,
            projection.BackdropUrl,
            projection.ReleaseDate,
            projection.VoteAverage,
            projection.VoteCount,
            projection.Year,
            projection.GenreIds,
            ToGenreDictionary(projection.GenreIds, projection.GenreNames),
            projection.PersonIds,
            projection.TmdbCollectionId);

        if (keywordIds is { Count: > 0 })
        {
            return profile with { KeywordIds = keywordIds };
        }

        return profile;
    }

    private static Dictionary<Guid, string> ToGenreDictionary(
        List<Guid> genreIds,
        List<string> genreNames)
    {
        var dictionary = new Dictionary<Guid, string>();
        for (var index = 0; index < genreIds.Count && index < genreNames.Count; index++)
        {
            dictionary[genreIds[index]] = genreNames[index];
        }

        return dictionary;
    }
}
