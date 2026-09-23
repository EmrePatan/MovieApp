using System.Net;
using Microsoft.Extensions.Options;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbLocalizedDetailDataProviderTests
{
    [Fact]
    public async Task GetMovieLocalizationAsync_UsesLocalizedLanguage_NotCanonicalDefault()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 157336,
                  "title": "Yıldızlararası",
                  "original_title": "Interstellar",
                  "overview": "Turkish overview"
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetMovieLocalizationAsync(
            157336,
            ContentLocaleResolver.TurkishTurkey);

        Assert.NotNull(result);
        Assert.Equal("Yıldızlararası", result.Title);
        var query = handler.Requests.Single().RequestUri?.Query ?? string.Empty;
        Assert.Contains("language=tr-TR", query, StringComparison.Ordinal);
        Assert.DoesNotContain("language=en-US", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMovieLocalizationAsync_UsesSpanishLanguage_ForSpanishLocale()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": 157336,
                  "title": "Interestelar",
                  "original_title": "Interstellar",
                  "overview": "Spanish overview"
                }
                """)
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetMovieLocalizationAsync(
            157336,
            ContentLocaleResolver.SpanishSpain);

        Assert.NotNull(result);
        Assert.Equal("Interestelar", result.Title);
        var query = handler.Requests.Single().RequestUri?.Query ?? string.Empty;
        Assert.Contains("language=es-ES", query, StringComparison.Ordinal);
        Assert.DoesNotContain("language=en-US", query, StringComparison.Ordinal);
    }

    private static TmdbLocalizedDetailDataProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        var apiClient = new TmdbApiClient(
            httpClient,
            Options.Create(new TmdbOptions
            {
                ApiKey = "test-api-key",
                CanonicalLanguage = "en-US"
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TmdbApiClient>.Instance);

        return new TmdbLocalizedDetailDataProvider(apiClient);
    }
}
