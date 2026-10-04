using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.AdvancedSearch;

internal static class AdvancedSearchBrowseCatalogSeed
{
    public static readonly Guid TopRatedMovieId = new("a1111111-1111-4111-8111-111111111101");

    public static readonly Guid TopRatedTvShowId = new("a2222222-2222-4222-8222-222222222202");

    public static async Task SeedTopRatedCatalogAsync(ApplicationDbContext context)
    {
        var utcNow = DateTime.UtcNow;
        context.Movies.Add(new Movie
        {
            Id = TopRatedMovieId,
            TmdbId = FakeDiscoverCatalog.DiscoverMovieAlphaTmdbId,
            Title = "Discover Movie Alpha",
            ReleaseDate = new DateOnly(2024, 6, 15),
            VoteAverage = 8.4m,
            VoteCount = 5000,
            OriginalLanguage = "en",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.TvShows.Add(new TvShow
        {
            Id = TopRatedTvShowId,
            TmdbId = FakeDiscoverCatalog.DiscoverTvAlphaTmdbId,
            Title = "Discover TV Alpha",
            FirstAirDate = new DateOnly(2024, 5, 1),
            VoteAverage = 8.8m,
            VoteCount = 4200,
            Status = TvShowStatus.ReturningSeries,
            OriginalLanguage = "en",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        await context.SaveChangesAsync();
    }

    public static readonly Guid HiddenGemMovieId = new("b1111111-1111-4111-8111-111111111101");

    public static async Task SeedHiddenGemsCatalogAsync(ApplicationDbContext context)
    {
        var utcNow = DateTime.UtcNow;
        context.Movies.Add(new Movie
        {
            Id = HiddenGemMovieId,
            TmdbId = FakeDiscoverCatalog.DiscoverMovieBetaTmdbId,
            Title = "Discover Hidden Gem",
            ReleaseDate = new DateOnly(2023, 3, 1),
            VoteAverage = 8.2m,
            VoteCount = 250,
            PosterPath = "/poster-hidden.jpg",
            OriginalLanguage = "en",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        await context.SaveChangesAsync();
    }
}
