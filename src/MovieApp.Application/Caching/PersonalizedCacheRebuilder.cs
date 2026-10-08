using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Insights;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Caching;

/// <summary>
/// Rebuilds the home and insights payloads the mobile app reads after a library write:
/// personalized home <c>type=all&amp;sectionSize=10&amp;releaseRegion=TR</c> for tr-TR and en-US,
/// then insights v3 for <c>Europe/Istanbul</c>. Each read captures the generation before it
/// builds and skips the cache write when that generation has moved on.
/// </summary>
public sealed class PersonalizedCacheRebuilder(
    IHomeService homeService,
    IInsightsV3Service insightsService,
    ILogger<PersonalizedCacheRebuilder> logger) : IPersonalizedCacheRebuilder
{
    public const int HomeSectionSize = 10;

    public const string ReleaseRegion = "TR";

    public const string InsightsTimeZoneId = "Europe/Istanbul";

    public static readonly string[] ContentLocales =
    [
        SupportedContentLocales.TurkishTurkey,
        SupportedContentLocales.EnglishUnitedStates
    ];

    public async Task RebuildAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using (CurrentUserAmbient.Push(userId))
        {
            foreach (var locale in ContentLocales)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await homeService.GetHomePersonalizedAsync(
                        new HomeCriteria(SearchContentType.All, HomeSectionSize),
                        locale,
                        ReleaseRegion,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    PersonalizedCacheRebuildLogMessages.LogRebuildFailed(logger, userId, exception);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await insightsService.GetInsightsV3Async(InsightsTimeZoneId, year: null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                PersonalizedCacheRebuildLogMessages.LogRebuildFailed(logger, userId, exception);
            }
        }

        stopwatch.Stop();
        PersonalizedCacheRebuildLogMessages.LogRebuildFinished(logger, userId, stopwatch.ElapsedMilliseconds);
    }
}
