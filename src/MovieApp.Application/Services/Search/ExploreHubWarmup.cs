using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Search;

/// <summary>
/// Fills the shared Keşfet caches the hub always requests, including the eight genre
/// rails. Genre ids are resolved from the canonical English names stored on the server.
/// </summary>
public static class ExploreHubWarmup
{
    public const int SectionSize = 10;

    public static readonly string[] ContentLocales =
    [
        SupportedContentLocales.TurkishTurkey,
        SupportedContentLocales.EnglishUnitedStates
    ];

    public static async Task WarmAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var preview = services.GetRequiredService<IExplorePreviewService>();
        foreach (var locale in ContentLocales)
        {
            await preview.GetPreviewAsync(new ExplorePreviewCriteria(SectionSize), locale, cancellationToken);
        }

        var watchProviders = services.GetRequiredService<IDiscoveryWatchProvidersService>();
        await watchProviders.GetWatchProvidersAsync(SearchContentType.Movie, "TR", cancellationToken);
        await watchProviders.GetWatchProvidersAsync(SearchContentType.Tv, "TR", cancellationToken);

        var genres = services.GetRequiredService<IGenreReadRepository>();
        var browse = services.GetRequiredService<IDiscoverBrowseService>();
        foreach (var name in GenreHubRailWarmup.CanonicalGenreNames)
        {
            var genreId = await genres.GetIdByNameAsync(name, cancellationToken);
            if (genreId is null)
            {
                continue;
            }

            var criteria = GenreHubRailWarmup.CreateCriteria(genreId.Value);
            foreach (var locale in ContentLocales)
            {
                await browse.BrowseAsync(criteria, locale, cancellationToken);
            }
        }
    }
}
