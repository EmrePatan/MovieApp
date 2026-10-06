using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class ProviderPreviewsBatchService(
    IServiceScopeFactory scopeFactory,
    ILogger<ProviderPreviewsBatchService> logger) : IProviderPreviewsBatchService
{
    public async Task<IReadOnlyList<ProviderPreviewGroupResult>> GetPreviewsAsync(
        ProviderPreviewsBatchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var validation = ProviderPreviewsBatchValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var results = await DiscoveryBatchOrchestration.ExecuteInOrderAsync(
            criteria.ProviderIds,
            DiscoveryBatchOrchestration.ProviderPreviewBatchMaxConcurrentOperations,
            (providerId, ct) => FetchProviderPreviewAsync(
                providerId,
                criteria,
                contentLocale,
                ct),
            cancellationToken);

        var transientErrorCount = results.Count(
            result => result.Status == DiscoveryBatchItemStatus.TransientError);
        if (transientErrorCount > 0 && transientErrorCount == results.Count)
        {
            throw new SearchProviderUnavailableException();
        }

        return results;
    }

    private async Task<ProviderPreviewGroupResult> FetchProviderPreviewAsync(
        int providerId,
        ProviderPreviewsBatchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var advancedDiscoverService = scope.ServiceProvider.GetRequiredService<IAdvancedDiscoverService>();

        var discoverCriteria = new AdvancedDiscoverCriteria(
            criteria.MediaType,
            [],
            GenreMatchMode.All,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            criteria.WatchRegion,
            [providerId],
            [WatchMonetizationType.Stream],
            [],
            [],
            AdvancedDiscoverSort.PopularityDesc,
            1,
            criteria.PageSize);

        try
        {
            var discoverResult = await advancedDiscoverService.DiscoverAsync(
                discoverCriteria,
                contentLocale,
                cancellationToken);

            return new ProviderPreviewGroupResult(
                providerId,
                DiscoveryBatchItemStatus.Ok,
                discoverResult.Items);
        }
        catch (SearchProviderUnavailableException exception)
        {
            DiscoveryBatchLogMessages.LogProviderPreviewUnavailable(logger, providerId, exception);

            return new ProviderPreviewGroupResult(
                providerId,
                DiscoveryBatchItemStatus.TransientError,
                []);
        }
    }
}
