using MovieApp.Application.Models.Videos;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

public static class TmdbVideosMapper
{
    public static IReadOnlyList<ProviderVideoResult> ToProviderVideoResults(TmdbVideosResponseJson response) =>
        response.Results
            .Select(ToProviderVideoResult)
            .ToList();

    private static ProviderVideoResult ToProviderVideoResult(TmdbVideoJson video) =>
        new(
            Site: video.Site,
            Type: video.Type,
            Key: video.Key,
            Name: video.Name,
            Official: video.Official,
            Language: video.Iso6391,
            Country: video.Iso31661,
            PublishedAt: video.PublishedAt);
}
