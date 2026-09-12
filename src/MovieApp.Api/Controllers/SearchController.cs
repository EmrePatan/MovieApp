using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.RateLimiting;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Search;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController(
    ISearchService searchService,
    IAutocompleteService autocompleteService) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting(SearchRateLimitPolicies.UnifiedSearch)]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? type,
        [FromQuery] Guid? genreId,
        [FromQuery] int? year,
        [FromQuery] decimal? minRating,
        [FromQuery] decimal? maxRating,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildSearchCriteria(
                query,
                page,
                pageSize,
                type,
                genreId,
                year,
                minRating,
                maxRating,
                sort);

            var result = await searchService.SearchAsync(criteria, cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid search request.",
                exception.Message));
        }
    }

    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(SearchAutocompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchAutocompleteResponse>> Autocomplete(
        [FromQuery(Name = "q")] string? query,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await autocompleteService.GetSuggestionsAsync(query ?? string.Empty, cancellationToken);
            return Ok(SearchContractMapper.ToAutocompleteResponse(items));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid autocomplete request.",
                exception.Message));
        }
    }

    internal static SearchCriteria BuildSearchCriteria(
        string? query,
        int? page,
        int? pageSize,
        string? type,
        Guid? genreId,
        int? year,
        decimal? minRating,
        decimal? maxRating,
        string? sort)
    {
        var typeValidation = AdvancedSearchValidator.ValidateType(type);
        if (!typeValidation.IsValid)
        {
            throw new ValidationException(typeValidation.ErrorMessage!);
        }

        var sortValidation = AdvancedSearchValidator.ValidateSort(sort);
        if (!sortValidation.IsValid)
        {
            throw new ValidationException(sortValidation.ErrorMessage!);
        }

        _ = AdvancedSearchValidator.TryParseType(type, out var contentType);
        _ = AdvancedSearchValidator.TryParseSort(sort, out var sortOption);

        return new SearchCriteria(
            query,
            contentType,
            genreId,
            year,
            minRating,
            maxRating,
            sortOption,
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}
