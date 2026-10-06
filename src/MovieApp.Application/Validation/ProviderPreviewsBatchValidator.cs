using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Validation;

public static class ProviderPreviewsBatchValidator
{
    public static SearchQueryValidationResult Validate(ProviderPreviewsBatchCriteria criteria)
    {
        if (criteria.ProviderIds.Count == 0)
        {
            return SearchQueryValidationResult.Failure("At least one provider ID is required.");
        }

        if (criteria.ProviderIds.Count > DiscoveryBatchOrchestration.MaxProviderPreviewBatchSize)
        {
            return SearchQueryValidationResult.Failure(
                $"A maximum of {DiscoveryBatchOrchestration.MaxProviderPreviewBatchSize} provider IDs is supported.");
        }

        var mediaTypeValidation = AdvancedDiscoverValidator.ValidateMediaType(criteria.MediaType.ToString().ToLowerInvariant());
        if (!mediaTypeValidation.IsValid)
        {
            return mediaTypeValidation;
        }

        if (criteria.MediaType is not (SearchContentType.Movie or SearchContentType.Tv))
        {
            return SearchQueryValidationResult.Failure("Media type must be movie or tv.");
        }

        var watchRegionValidation = AdvancedDiscoverValidator.ValidateWatchRegion(criteria.WatchRegion, required: true);
        if (!watchRegionValidation.IsValid)
        {
            return watchRegionValidation;
        }

        foreach (var providerId in criteria.ProviderIds)
        {
            if (providerId < AdvancedDiscoverValidator.MinimumWatchProviderId ||
                providerId > AdvancedDiscoverValidator.MaximumWatchProviderId)
            {
                return SearchQueryValidationResult.Failure("Watch provider IDs are invalid.");
            }
        }

        if (criteria.PageSize < DiscoveryBatchOrchestration.MinProviderPreviewPageSize ||
            criteria.PageSize > DiscoveryBatchOrchestration.MaxProviderPreviewPageSize)
        {
            return SearchQueryValidationResult.Failure(
                $"Page size must be between {DiscoveryBatchOrchestration.MinProviderPreviewPageSize} and {DiscoveryBatchOrchestration.MaxProviderPreviewPageSize}.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateRequest(
        IReadOnlyList<int>? providerIds,
        string? mediaType,
        string? watchRegion,
        int? pageSize)
    {
        if (providerIds is null)
        {
            return SearchQueryValidationResult.Failure("Request body is required.");
        }

        var normalizedProviderIds = NormalizeOrderedProviderIds(providerIds, out var providerValidation);
        if (!providerValidation.IsValid)
        {
            return providerValidation;
        }

        var mediaTypeValidation = AdvancedDiscoverValidator.ValidateMediaType(mediaType);
        if (!mediaTypeValidation.IsValid)
        {
            return mediaTypeValidation;
        }

        var watchRegionValidation = AdvancedDiscoverValidator.ValidateWatchRegion(watchRegion, required: true);
        if (!watchRegionValidation.IsValid)
        {
            return watchRegionValidation;
        }

        var resolvedPageSize = pageSize ?? DiscoveryBatchOrchestration.DefaultProviderPreviewPageSize;

        _ = AdvancedSearchValidator.TryParseType(mediaType, out var contentType);

        return Validate(new ProviderPreviewsBatchCriteria(
            normalizedProviderIds,
            contentType,
            WatchProviderRegionValidator.Normalize(watchRegion!),
            resolvedPageSize));
    }

    public static ProviderPreviewsBatchCriteria CreateCriteria(
        IReadOnlyList<int>? providerIds,
        string? mediaType,
        string? watchRegion,
        int? pageSize)
    {
        var validation = ValidateRequest(providerIds, mediaType, watchRegion, pageSize);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var normalizedProviderIds = NormalizeOrderedProviderIds(providerIds!, out _);
        _ = AdvancedSearchValidator.TryParseType(mediaType, out var contentType);
        var resolvedPageSize = pageSize ?? DiscoveryBatchOrchestration.DefaultProviderPreviewPageSize;

        return new ProviderPreviewsBatchCriteria(
            normalizedProviderIds,
            contentType,
            WatchProviderRegionValidator.Normalize(watchRegion!),
            resolvedPageSize);
    }

    public static IReadOnlyList<int> NormalizeOrderedProviderIds(
        IReadOnlyList<int> providerIds,
        out SearchQueryValidationResult validation)
    {
        validation = SearchQueryValidationResult.Success();
        var seen = new HashSet<int>();
        var ordered = new List<int>();

        foreach (var providerId in providerIds)
        {
            if (providerId <= 0)
            {
                validation = SearchQueryValidationResult.Failure("Watch provider IDs are invalid.");
                return [];
            }

            if (seen.Add(providerId))
            {
                ordered.Add(providerId);
            }
        }

        if (ordered.Count == 0)
        {
            validation = SearchQueryValidationResult.Failure("At least one provider ID is required.");
        }

        return ordered;
    }
}
