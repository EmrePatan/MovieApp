using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeCollectionDataProvider : ICollectionDataProvider
{
    public const int SpaceOdysseyCollectionId = 10001;
    public const int UnknownCollectionId = 999999;

    public bool ShouldThrow { get; set; }

    public Task<CollectionProviderDetails?> GetCollectionAsync(
        int tmdbCollectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ShouldThrow)
        {
            throw new InvalidOperationException("Simulated collection provider failure.");
        }

        if (tmdbCollectionId == SpaceOdysseyCollectionId)
        {
            return Task.FromResult<CollectionProviderDetails?>(CreateSpaceOdysseyCollection());
        }

        return Task.FromResult<CollectionProviderDetails?>(null);
    }

    internal static CollectionProviderDetails CreateSpaceOdysseyCollection() =>
        new(
            SpaceOdysseyCollectionId,
            "Space Odyssey Collection",
            "A curated fake collection for tests.",
            "/fake/collection-poster.jpg",
            "/fake/collection-backdrop.jpg",
            [
                new CollectionProviderPart(
                    FakeMovieDataProvider.InterstellarTmdbId,
                    "Interstellar",
                    "Interstellar",
                    "Overview",
                    "/fake/interstellar-poster.jpg",
                    "/fake/interstellar-backdrop.jpg",
                    new DateOnly(2014, 11, 7),
                    8.7m,
                    25000,
                    Adult: false),
                new CollectionProviderPart(
                    FakeMovieDataProvider.InterstellarTmdbId,
                    "Interstellar Duplicate",
                    "Interstellar Duplicate",
                    "Duplicate overview",
                    "/fake/interstellar-duplicate-poster.jpg",
                    null,
                    new DateOnly(2014, 11, 7),
                    8.0m,
                    100,
                    Adult: false),
                new CollectionProviderPart(
                    920001,
                    "Adult Entry",
                    "Adult Entry",
                    "Should be filtered out.",
                    null,
                    null,
                    new DateOnly(2010, 1, 1),
                    1.0m,
                    1,
                    Adult: true),
                new CollectionProviderPart(
                    920002,
                    "Future Mission",
                    "Future Mission",
                    "Future overview",
                    "/fake/future-poster.jpg",
                    null,
                    new DateOnly(2099, 1, 1),
                    0m,
                    0,
                    Adult: false),
                new CollectionProviderPart(
                    920003,
                    "Untitled Prelude",
                    "Untitled Prelude",
                    null,
                    null,
                    null,
                    null,
                    6.5m,
                    12,
                    Adult: false),
                new CollectionProviderPart(
                    0,
                    "Malformed Part",
                    null,
                    null,
                    null,
                    null,
                    null,
                    0m,
                    0,
                    Adult: false),
                new CollectionProviderPart(
                    920004,
                    "   ",
                    "   ",
                    null,
                    null,
                    null,
                    new DateOnly(2001, 1, 1),
                    7.0m,
                    50,
                    Adult: false)
            ]);
}
