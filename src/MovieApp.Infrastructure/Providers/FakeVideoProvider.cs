using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Videos;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeVideoProvider : IVideoProvider
{
    public static readonly IReadOnlyList<ProviderVideoResult> InterstellarVideos =
    [
        new(
            Site: "YouTube",
            Type: "Trailer",
            Key: "fake-interstellar-trailer",
            Name: "Official Trailer",
            Official: true,
            Language: "en",
            Country: "US",
            PublishedAt: new DateTimeOffset(2014, 10, 1, 0, 0, 0, TimeSpan.Zero))
    ];

    public static readonly IReadOnlyList<ProviderVideoResult> BreakingBadVideos =
    [
        new(
            Site: "YouTube",
            Type: "Trailer",
            Key: "fake-breaking-bad-trailer",
            Name: "Official Trailer",
            Official: true,
            Language: "en",
            Country: "US",
            PublishedAt: new DateTimeOffset(2008, 1, 1, 0, 0, 0, TimeSpan.Zero))
    ];

    public Task<IReadOnlyList<ProviderVideoResult>> GetMovieVideosAsync(
        int tmdbId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProviderVideoResult>>(
            tmdbId == FakeMovieDataProvider.InterstellarTmdbId ? InterstellarVideos : []);

    public Task<IReadOnlyList<ProviderVideoResult>> GetTvShowVideosAsync(
        int tmdbId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProviderVideoResult>>(
            tmdbId == FakeTvShowDataProvider.BreakingBadTmdbId ? BreakingBadVideos : []);
}
