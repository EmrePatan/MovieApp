using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Images;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeImageProvider : IImageProvider
{
    public static readonly ProviderImagesResult InterstellarImages =
        new(
            Backdrops:
            [
                new("/fake/interstellar-backdrop-en.jpg", "en", 1.778m, 1920, 1080, 8.5m, 12),
                new("/fake/interstellar-backdrop-neutral.jpg", null, 1.778m, 1920, 1080, 7.0m, 4)
            ],
            Posters:
            [
                new("/fake/interstellar-poster-en.jpg", "en", 0.667m, 1000, 1500, 9.0m, 20),
                new("/fake/interstellar-poster-fr.jpg", "fr", 0.667m, 1000, 1500, 6.0m, 3)
            ],
            Logos:
            [
                new("/fake/interstellar-logo.png", "en", 3.0m, 600, 200, 5.0m, 2)
            ],
            Profiles: []);

    public static readonly ProviderImagesResult BreakingBadImages =
        new(
            Backdrops:
            [
                new("/fake/breaking-bad-backdrop.jpg", "en", 1.778m, 1920, 1080, 8.0m, 10)
            ],
            Posters:
            [
                new("/fake/breaking-bad-poster.jpg", "en", 0.667m, 1000, 1500, 8.5m, 15)
            ],
            Logos: [],
            Profiles: []);

    public static readonly ProviderImagesResult McConaugheyImages =
        new(
            Backdrops: [],
            Posters: [],
            Logos: [],
            Profiles:
            [
                new("/fake/cooper-profile-1.jpg", "en", 0.667m, 1000, 1500, 9.5m, 30),
                new("/fake/cooper-profile-2.jpg", "en", 0.667m, 1000, 1500, 7.0m, 10)
            ]);

    public Task<ProviderImagesResult?> GetMovieImagesAsync(
        int tmdbId,
        string? language,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProviderImagesResult?>(
            tmdbId == FakeMovieDataProvider.InterstellarTmdbId ? InterstellarImages : null);

    public Task<ProviderImagesResult?> GetTvShowImagesAsync(
        int tmdbId,
        string? language,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProviderImagesResult?>(
            tmdbId == FakeTvShowDataProvider.BreakingBadTmdbId ? BreakingBadImages : null);

    public Task<ProviderImagesResult?> GetPersonImagesAsync(
        int tmdbPersonId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProviderImagesResult?>(
            tmdbPersonId == FakePersonDataProvider.McConaugheyTmdbId ? McConaugheyImages : null);
}
