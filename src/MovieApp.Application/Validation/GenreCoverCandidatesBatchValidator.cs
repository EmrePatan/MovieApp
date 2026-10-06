using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Validation;

public static class GenreCoverCandidatesBatchValidator
{
    public static SearchQueryValidationResult Validate(GenreCoverCandidatesBatchCriteria criteria)
    {
        if (criteria.GenreIds.Count == 0)
        {
            return SearchQueryValidationResult.Failure("At least one genre ID is required.");
        }

        if (criteria.GenreIds.Count > DiscoveryBatchOrchestration.MaxGenreCoverBatchSize)
        {
            return SearchQueryValidationResult.Failure(
                $"A maximum of {DiscoveryBatchOrchestration.MaxGenreCoverBatchSize} genre IDs is supported.");
        }

        var typeValidation = AdvancedSearchValidator.ValidateType(criteria.MediaType.ToString().ToLowerInvariant());
        if (!typeValidation.IsValid)
        {
            return typeValidation;
        }

        if (criteria.CandidateCount < DiscoveryBatchOrchestration.MinGenreCoverCandidatesPerGenre ||
            criteria.CandidateCount > DiscoveryBatchOrchestration.MaxGenreCoverCandidatesPerGenre)
        {
            return SearchQueryValidationResult.Failure(
                $"Candidate count must be between {DiscoveryBatchOrchestration.MinGenreCoverCandidatesPerGenre} and {DiscoveryBatchOrchestration.MaxGenreCoverCandidatesPerGenre}.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateRequest(
        IReadOnlyList<string>? genreIds,
        string? mediaType,
        int? candidateCount)
    {
        if (genreIds is null)
        {
            return SearchQueryValidationResult.Failure("Request body is required.");
        }

        var normalizedGenreIds = NormalizeOrderedGenreIds(genreIds, out var genreValidation);
        if (!genreValidation.IsValid)
        {
            return genreValidation;
        }

        var typeValidation = AdvancedSearchValidator.ValidateType(mediaType);
        if (!typeValidation.IsValid)
        {
            return typeValidation;
        }

        var resolvedCandidateCount =
            candidateCount ?? DiscoveryBatchOrchestration.MaxGenreCoverCandidatesPerGenre;

        if (resolvedCandidateCount < DiscoveryBatchOrchestration.MinGenreCoverCandidatesPerGenre ||
            resolvedCandidateCount > DiscoveryBatchOrchestration.MaxGenreCoverCandidatesPerGenre)
        {
            return SearchQueryValidationResult.Failure(
                $"Candidate count must be between {DiscoveryBatchOrchestration.MinGenreCoverCandidatesPerGenre} and {DiscoveryBatchOrchestration.MaxGenreCoverCandidatesPerGenre}.");
        }

        _ = AdvancedSearchValidator.TryParseType(mediaType, out var contentType);

        return Validate(new GenreCoverCandidatesBatchCriteria(
            normalizedGenreIds,
            contentType,
            resolvedCandidateCount));
    }

    public static GenreCoverCandidatesBatchCriteria CreateCriteria(
        IReadOnlyList<string>? genreIds,
        string? mediaType,
        int? candidateCount)
    {
        var validation = ValidateRequest(genreIds, mediaType, candidateCount);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var normalizedGenreIds = NormalizeOrderedGenreIds(genreIds!, out _);
        _ = AdvancedSearchValidator.TryParseType(mediaType, out var contentType);
        var resolvedCandidateCount =
            candidateCount ?? DiscoveryBatchOrchestration.MaxGenreCoverCandidatesPerGenre;

        return new GenreCoverCandidatesBatchCriteria(
            normalizedGenreIds,
            contentType,
            resolvedCandidateCount);
    }

    public static IReadOnlyList<Guid> NormalizeOrderedGenreIds(
        IReadOnlyList<string> genreIds,
        out SearchQueryValidationResult validation)
    {
        validation = SearchQueryValidationResult.Success();
        var seen = new HashSet<Guid>();
        var ordered = new List<Guid>();

        foreach (var rawValue in genreIds)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                validation = SearchQueryValidationResult.Failure("Genre ID cannot be empty.");
                return [];
            }

            if (!Guid.TryParse(rawValue.Trim(), out var genreId) || genreId == Guid.Empty)
            {
                validation = SearchQueryValidationResult.Failure("Genre ID must be a valid identifier.");
                return [];
            }

            if (seen.Add(genreId))
            {
                ordered.Add(genreId);
            }
        }

        if (ordered.Count == 0)
        {
            validation = SearchQueryValidationResult.Failure("At least one genre ID is required.");
        }

        return ordered;
    }
}
