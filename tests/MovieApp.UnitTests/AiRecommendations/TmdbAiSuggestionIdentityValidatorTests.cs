using System.Globalization;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.AiRecommendations;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class TmdbAiSuggestionIdentityValidatorTests
{
    [Fact]
    public void MatchesMovieAcceptsCanonicalTitle()
    {
        var details = CreateMovieDetails(42, "Arrival", "Arrival", 2016);
        var suggestion = new AiProviderSuggestion("Arrival", 2016, "movie", 42, "Reason");

        Assert.True(TmdbAiSuggestionIdentityValidator.MatchesMovie(suggestion, details));
    }

    [Fact]
    public void MatchesTvShowAcceptsLocalizedTitleFromProviderSearchTitles()
    {
        var details = CreateTvDetails(
            67750,
            "Insider",
            null,
            2016,
            [new ProviderSearchTitleEntry("İçerde", ContentSearchTitleKind.Translation, ContentSearchTitleSource.TmdbTranslation, "tr", "TR", null)]);
        var suggestion = new AiProviderSuggestion("İçerde", 2016, "tv", 67750, "Reason");

        Assert.True(TmdbAiSuggestionIdentityValidator.MatchesTvShow(suggestion, details));
    }

    [Fact]
    public void MatchesTvShowRejectsUnrelatedSuggestionForVoiceOfRomaniaMetadata()
    {
        var details = CreateTvDetails(56676, "The Voice of Romania", "Vocea României", 2011, []);
        var suggestion = new AiProviderSuggestion("İçerde", 2016, "tv", 56676, "Reason");

        Assert.False(TmdbAiSuggestionIdentityValidator.MatchesTvShow(suggestion, details));
    }

    private static MovieProviderDetails CreateMovieDetails(
        int tmdbId,
        string title,
        string? originalTitle,
        int year) =>
        new(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            tmdbId,
            null,
            null,
            title,
            originalTitle,
            "Overview",
            new DateOnly(year, 1, 1),
            116,
            null,
            null,
            "en",
            7m,
            100,
            ["Drama"]);

    private static TvShowProviderDetails CreateTvDetails(
        int tmdbId,
        string title,
        string? originalTitle,
        int year,
        IReadOnlyList<ProviderSearchTitleEntry> searchTitles) =>
        new(
            tmdbId.ToString(CultureInfo.InvariantCulture),
            tmdbId,
            null,
            null,
            title,
            originalTitle,
            "Overview",
            new DateOnly(year, 1, 1),
            null,
            null,
            null,
            "tr",
            8m,
            100,
            "Ended",
            ["Drama"],
            [],
            ProviderSearchTitles: searchTitles);
}
