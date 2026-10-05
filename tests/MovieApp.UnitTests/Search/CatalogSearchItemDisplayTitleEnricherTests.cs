using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.UnitTests.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Search;

public sealed class CatalogSearchItemDisplayTitleEnricherTests
{
    private static readonly Guid UsMovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TurkishMovieId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid KoreanMovieId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid FrenchMovieId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task TurkishLocale_ForeignEnglishProduction_UsesEnglishPrimaryAndTurkishSubtitle()
    {
        var item = CreateMovieItem(UsMovieId, "Spider-Man", "Spider-Man");
        var enricher = CreateEnricher(
            localizedTitles: new Dictionary<CatalogContentKey, string>
            {
                [new CatalogContentKey(UsMovieId, "movie")] = "Örümcek Adam"
            },
            movieContexts: new Dictionary<Guid, ContentProductionContext>
            {
                [UsMovieId] = new("en", "US")
            });

        var result = await enricher.EnrichAsync(
            new PaginatedResult<SearchItem>([item], 1, 20, 1, 1),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Spider-Man", result.Items[0].Title);
        Assert.Equal("Örümcek Adam", result.Items[0].OriginalTitle);
    }

    [Fact]
    public async Task TurkishLocale_TurkishProduction_UsesTurkishPrimaryAndEnglishSubtitle()
    {
        var item = CreateMovieItem(TurkishMovieId, "Flames of Fate", "Alev Alev");
        var enricher = CreateEnricher(
            localizedTitles: new Dictionary<CatalogContentKey, string>
            {
                [new CatalogContentKey(TurkishMovieId, "movie")] = "Alev Alev"
            },
            movieContexts: new Dictionary<Guid, ContentProductionContext>
            {
                [TurkishMovieId] = new("tr", "TR")
            });

        var result = await enricher.EnrichAsync(
            new PaginatedResult<SearchItem>([item], 1, 20, 1, 1),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Alev Alev", result.Items[0].Title);
        Assert.Equal("Flames of Fate", result.Items[0].OriginalTitle);
    }

    [Fact]
    public async Task TurkishLocale_KoreanProduction_UsesCanonicalPrimaryAndTurkishSubtitle()
    {
        var item = CreateMovieItem(KoreanMovieId, "Parasite", "Gisaengchung");
        var enricher = CreateEnricher(
            localizedTitles: new Dictionary<CatalogContentKey, string>
            {
                [new CatalogContentKey(KoreanMovieId, "movie")] = "Parazit"
            },
            movieContexts: new Dictionary<Guid, ContentProductionContext>
            {
                [KoreanMovieId] = new("ko", "KR")
            });

        var result = await enricher.EnrichAsync(
            new PaginatedResult<SearchItem>([item], 1, 20, 1, 1),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Parasite", result.Items[0].Title);
        Assert.Equal("Parazit", result.Items[0].OriginalTitle);
    }

    [Fact]
    public async Task TurkishLocale_EuropeanProduction_UsesCanonicalPrimaryAndTurkishSubtitle()
    {
        var item = CreateMovieItem(FrenchMovieId, "Amélie", "Le Fabuleux Destin d'Amélie Poulain");
        var enricher = CreateEnricher(
            localizedTitles: new Dictionary<CatalogContentKey, string>
            {
                [new CatalogContentKey(FrenchMovieId, "movie")] = "Amélie'nin Muhteşem Dünyası"
            },
            movieContexts: new Dictionary<Guid, ContentProductionContext>
            {
                [FrenchMovieId] = new("fr", "FR")
            });

        var result = await enricher.EnrichAsync(
            new PaginatedResult<SearchItem>([item], 1, 20, 1, 1),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Amélie", result.Items[0].Title);
        Assert.Equal("Amélie'nin Muhteşem Dünyası", result.Items[0].OriginalTitle);
    }

    [Fact]
    public async Task TurkishLocale_ForeignProductionWithoutLocalization_KeepsCanonicalPrimary()
    {
        var item = CreateMovieItem(UsMovieId, "Unknown Film", "Original Name");
        var enricher = CreateEnricher(
            localizedTitles: new Dictionary<CatalogContentKey, string>(),
            movieContexts: new Dictionary<Guid, ContentProductionContext>
            {
                [UsMovieId] = new("en", "US")
            });

        var result = await enricher.EnrichAsync(
            new PaginatedResult<SearchItem>([item], 1, 20, 1, 1),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Unknown Film", result.Items[0].Title);
        Assert.Equal("Original Name", result.Items[0].OriginalTitle);
    }

    [Fact]
    public async Task MislocalizedProviderTitle_AsPrimaryInput_ProducesWrongDisplayTitles()
    {
        var item = CreateMovieItem(UsMovieId, "Örümcek Adam", "Spider-Man");
        var enricher = CreateEnricher(
            localizedTitles: new Dictionary<CatalogContentKey, string>
            {
                [new CatalogContentKey(UsMovieId, "movie")] = "Örümcek Adam"
            },
            movieContexts: new Dictionary<Guid, ContentProductionContext>
            {
                [UsMovieId] = new("en", "US")
            });

        var result = await enricher.EnrichAsync(
            new PaginatedResult<SearchItem>([item], 1, 20, 1, 1),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Örümcek Adam", result.Items[0].Title);
        Assert.Equal("Spider-Man", result.Items[0].OriginalTitle);
    }

    private static SearchItem CreateMovieItem(Guid id, string title, string? originalTitle) =>
        new(
            id,
            "movie",
            title,
            originalTitle,
            "Overview",
            "/poster.jpg",
            null,
            new DateOnly(2020, 1, 1),
            8m,
            1000,
            2020,
            55_001);

    private static CatalogSearchItemDisplayTitleEnricher CreateEnricher(
        IReadOnlyDictionary<CatalogContentKey, string> localizedTitles,
        IReadOnlyDictionary<Guid, ContentProductionContext> movieContexts) =>
        new(new ConfiguredSummaryLocalizationMetadataReadRepository(localizedTitles, movieContexts));
}
