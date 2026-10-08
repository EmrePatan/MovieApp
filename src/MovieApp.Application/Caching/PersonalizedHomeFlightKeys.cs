using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class PersonalizedHomeFlightKeys
{
    public static string Create(
        Guid userId,
        SearchContentType type,
        int sectionSize,
        string contentLocale,
        string releaseRegion,
        long generation) =>
        $"home-personalized:{userId:N}:{type}:{sectionSize}:{contentLocale}:{releaseRegion}:g{generation}";
}
