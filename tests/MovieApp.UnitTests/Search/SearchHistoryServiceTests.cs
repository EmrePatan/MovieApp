using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Search;

public sealed class SearchHistoryServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task GetHistoryAsyncReturnsUserHistory()
    {
        var history = SearchHistory.Create(UserId, "Batman", "batman", DateTime.UtcNow);
        var service = CreateService(new FakeSearchHistoryRepository([history], 1));

        var result = await service.GetHistoryAsync(1, 20);

        Assert.Single(result.Items);
        Assert.Equal("Batman", result.Items[0].Query);
    }

    [Fact]
    public async Task DeleteHistoryItemAsyncThrowsWhenMissing()
    {
        var service = CreateService(new FakeSearchHistoryRepository([], 0, deleteReturns: false));

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteHistoryItemAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetHistoryAsyncThrowsWhenNotAuthenticated()
    {
        var service = CreateService(new FakeSearchHistoryRepository([], 0), new FakeCurrentUser(null));

        await Assert.ThrowsAsync<AuthenticationException>(() => service.GetHistoryAsync(1, 20));
    }

    private static SearchHistoryService CreateService(
        FakeSearchHistoryRepository repository,
        ICurrentUser? currentUser = null) =>
        new(currentUser ?? new FakeCurrentUser(UserId), repository);

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FakeSearchHistoryRepository(
        IReadOnlyList<SearchHistory> items,
        int totalCount,
        bool deleteReturns = true) : ISearchHistoryRepository
    {
        public Task RecordSearchAsync(
            Guid userId,
            string query,
            string normalizedQuery,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(IReadOnlyList<SearchHistory> Items, int TotalCount)> GetUserHistoryAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((items, totalCount));

        public Task<bool> DeleteAsync(Guid userId, Guid historyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(deleteReturns);

        public Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
