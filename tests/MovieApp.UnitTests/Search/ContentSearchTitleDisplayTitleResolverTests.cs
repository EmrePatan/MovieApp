using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Search;

public sealed class ContentSearchTitleDisplayTitleResolverTests
{
    private static readonly Guid MovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TvId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void TurkishTranslationWinsOverRegionalAlternative()
    {
        var scope = CatalogSearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.TurkishTurkey);
        var rows = new[]
        {
            CreateRow(CatalogContentType.Movie, MovieId, ContentSearchTitleKind.Alternative, "US Title", countryCode: "US"),
            CreateRow(CatalogContentType.Movie, MovieId, ContentSearchTitleKind.Translation, "Türkçe Ad", languageCode: "tr"),
        };

        var titles = ContentSearchTitleDisplayTitleResolver.BuildMovieDisplayTitles(rows, scope);

        Assert.Equal("Türkçe Ad", titles[new CatalogContentKey(MovieId, "movie")]);
    }

    [Fact]
    public void TurkishRegionalAlternativeUsedWhenTranslationMissing()
    {
        var scope = CatalogSearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.TurkishTurkey);
        var rows = new[]
        {
            CreateRow(CatalogContentType.Movie, MovieId, ContentSearchTitleKind.Alternative, "TR Alt", countryCode: "TR"),
            CreateRow(CatalogContentType.Movie, MovieId, ContentSearchTitleKind.Alternative, "US Alt", countryCode: "US"),
        };

        var titles = ContentSearchTitleDisplayTitleResolver.BuildMovieDisplayTitles(rows, scope);

        Assert.Equal("TR Alt", titles[new CatalogContentKey(MovieId, "movie")]);
    }

    [Fact]
    public void EnglishLocaleExcludesNonEnglishTranslationRows()
    {
        var scope = CatalogSearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.EnglishUnitedStates);
        var rows = new[]
        {
            CreateRow(CatalogContentType.Tv, TvId, ContentSearchTitleKind.Translation, "Turkish", languageCode: "tr"),
            CreateRow(CatalogContentType.Tv, TvId, ContentSearchTitleKind.Alternative, "US Alt", countryCode: "US"),
        };

        var titles = ContentSearchTitleDisplayTitleResolver.BuildTvDisplayTitles(rows, scope);

        Assert.Equal("US Alt", titles[new CatalogContentKey(TvId, "tv")]);
    }

    [Fact]
    public void MixedMovieAndTvBatchesResolveIndependently()
    {
        var scope = CatalogSearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.TurkishTurkey);
        var rows = new[]
        {
            CreateRow(CatalogContentType.Movie, MovieId, ContentSearchTitleKind.Translation, "Film", languageCode: "tr"),
            CreateRow(CatalogContentType.Tv, TvId, ContentSearchTitleKind.Translation, "Dizi", languageCode: "tr"),
        };

        var movieTitles = ContentSearchTitleDisplayTitleResolver.BuildMovieDisplayTitles(rows, scope);
        var tvTitles = ContentSearchTitleDisplayTitleResolver.BuildTvDisplayTitles(rows, scope);

        Assert.Equal("Film", movieTitles[new CatalogContentKey(MovieId, "movie")]);
        Assert.Equal("Dizi", tvTitles[new CatalogContentKey(TvId, "tv")]);
        Assert.False(movieTitles.ContainsKey(new CatalogContentKey(TvId, "tv")));
    }

    private static ContentSearchTitle CreateRow(
        CatalogContentType contentType,
        Guid contentId,
        ContentSearchTitleKind kind,
        string title,
        string? languageCode = null,
        string? countryCode = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ContentType = contentType,
            ContentId = contentId,
            TitleKind = kind,
            Title = title,
            NormalizedTitle = title.ToLowerInvariant(),
            LanguageCode = languageCode,
            CountryCode = countryCode,
            Source = ContentSearchTitleSource.TmdbTranslation,
        };
}
