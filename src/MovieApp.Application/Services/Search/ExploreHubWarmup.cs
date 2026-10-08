using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Search;

/// <summary>
/// Fills the shared Keşfet caches the hub always requests. Genre rails stay out of this
/// list: their catalog ids are chosen by the client and are not stored on the server.
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
    }
}
