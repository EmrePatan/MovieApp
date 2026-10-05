using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Localization;

[Collection("CatalogPersistence")]
public sealed class SummaryLocalizationMetadataBatchIntegrationTests
{
    [Fact]
    public async Task BatchLoaderMatchesSeparateTitleAndProductionReadsForTurkishLocale()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movieId = Guid.NewGuid();
        var tvId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Canonical Movie",
            OriginalTitle = "Original Movie",
            OriginalLanguage = "en",
            PrimaryOriginCountryCode = "US",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.TvShows.Add(new TvShow
        {
            Id = tvId,
            Title = "Canonical Tv",
            OriginalTitle = "Original Tv",
            OriginalLanguage = "en",
            PrimaryOriginCountryCode = "US",
            FirstAirDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.ContentSearchTitles.AddRange(
            CreateTranslationRow(CatalogContentType.Movie, movieId, "tr", "Film TR"),
            CreateTranslationRow(CatalogContentType.Tv, tvId, "tr", "Dizi TR"));
        await context.SaveChangesAsync();

        var titleRepository = new ContentSearchTitleReadRepository(context);
        var batchRepository = new SummaryLocalizationMetadataReadRepository(
            context,
            new ContentLocalizedPosterRepository(context));
        const string locale = ContentLocaleResolver.TurkishTurkey;

        var expectedMovieTitles = await titleRepository.GetLocaleDisplayTitlesAsync(
            CatalogContentType.Movie,
            [movieId],
            locale);
        var expectedTvTitles = await titleRepository.GetLocaleDisplayTitlesAsync(
            CatalogContentType.Tv,
            [tvId],
            locale);

        var batch = await batchRepository.LoadAsync(
            [movieId],
            [tvId],
            [],
            locale);

        Assert.Equal(expectedMovieTitles, batch.LocalizedMovieTitles);
        Assert.Equal(expectedTvTitles, batch.LocalizedTvTitles);
        Assert.Equal("en", batch.MovieProductionContexts[movieId].OriginalLanguage);
        Assert.Equal("US", batch.MovieProductionContexts[movieId].PrimaryOriginCountryCode);
        Assert.Equal("en", batch.TvProductionContexts[tvId].OriginalLanguage);
    }

    private static ContentSearchTitle CreateTranslationRow(
        CatalogContentType contentType,
        Guid contentId,
        string languageCode,
        string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            ContentType = contentType,
            ContentId = contentId,
            TitleKind = ContentSearchTitleKind.Translation,
            Title = title,
            NormalizedTitle = title.ToLowerInvariant(),
            LanguageCode = languageCode,
            Source = ContentSearchTitleSource.TmdbTranslation,
        };
}
