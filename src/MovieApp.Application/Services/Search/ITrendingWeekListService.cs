using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface ITrendingWeekListService
{
    /// <summary>
    /// TMDB trending/all/week, movie and TV titles only, in provider order.
    /// <see cref="PaginatedResult{T}.TotalPages"/> is the provider page count.
    /// </summary>
    Task<PaginatedResult<SearchItem>> GetPageAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
