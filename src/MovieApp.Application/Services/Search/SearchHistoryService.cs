using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class SearchHistoryService(
    ICurrentUser currentUser,
    ISearchHistoryRepository searchHistoryRepository) : ISearchHistoryService
{
    public async Task<PaginatedResult<SearchHistoryItem>> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var validation = AdvancedSearchValidator.ValidatePagination(page, pageSize);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var (items, totalCount) = await searchHistoryRepository.GetUserHistoryAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<SearchHistoryItem>(
            items.Select(item => new SearchHistoryItem(item.Id, item.Query, item.SearchedAt)).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await searchHistoryRepository.DeleteAllAsync(userId, cancellationToken);
    }

    public async Task DeleteHistoryItemAsync(Guid historyId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = await searchHistoryRepository.DeleteAsync(userId, historyId, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The requested search history entry was not found.");
        }
    }
}
