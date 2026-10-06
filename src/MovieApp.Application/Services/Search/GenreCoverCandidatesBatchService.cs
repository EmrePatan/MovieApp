using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class GenreCoverCandidatesBatchService(
    IServiceScopeFactory scopeFactory,
    ILogger<GenreCoverCandidatesBatchService> logger) : IGenreCoverCandidatesBatchService
{
    public async Task<IReadOnlyList<GenreCoverCandidatesGroupResult>> GetCandidatesAsync(
        GenreCoverCandidatesBatchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var validation = GenreCoverCandidatesBatchValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var concurrency = DiscoveryBatchOrchestration.ResolveGenreCoverBatchConcurrency(
            criteria.MediaType,
            contentLocale);

        var results = await DiscoveryBatchOrchestration.ExecuteInOrderAsync(
            criteria.GenreIds,
            concurrency,
            (genreId, ct) => FetchGenreCandidatesAsync(
                genreId,
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

    private async Task<GenreCoverCandidatesGroupResult> FetchGenreCandidatesAsync(
        Guid genreId,
        GenreCoverCandidatesBatchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var browseService = scope.ServiceProvider.GetRequiredService<IDiscoverBrowseService>();

        var browseCriteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.Popular,
            criteria.MediaType,
            [genreId],
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
            [],
            null,
            1,
            criteria.CandidateCount);

        try
        {
            var browseResult = await browseService.BrowseAsync(
                browseCriteria,
                contentLocale,
                cancellationToken);

            return new GenreCoverCandidatesGroupResult(
                genreId,
                DiscoveryBatchItemStatus.Ok,
                browseResult.Items);
        }
        catch (SearchProviderUnavailableException exception)
        {
            DiscoveryBatchLogMessages.LogGenreCoverBrowseProviderUnavailable(logger, genreId, exception);

            return new GenreCoverCandidatesGroupResult(
                genreId,
                DiscoveryBatchItemStatus.TransientError,
                []);
        }
    }
}
