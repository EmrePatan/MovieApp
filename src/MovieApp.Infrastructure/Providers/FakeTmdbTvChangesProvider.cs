using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.TvShowChanges;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeTmdbTvChangesProvider
{
    private readonly Dictionary<(DateOnly Start, DateOnly End), List<List<int>>> _windowPages = new();

    public bool FailNextPage { get; set; }

    public int GetTvChangesCallCount { get; private set; }

    public void ConfigureWindow(DateOnly startDate, DateOnly endDate, params int[][] pages)
    {
        _windowPages[(startDate, endDate)] = pages.Select(page => page.ToList()).ToList();
    }

    public void Reset()
    {
        _windowPages.Clear();
        FailNextPage = false;
        GetTvChangesCallCount = 0;
    }

    public Task<TmdbTvChangesPageResult> GetTvChangesPageAsync(
        DateOnly startDate,
        DateOnly endDate,
        int page,
        CancellationToken cancellationToken = default)
    {
        GetTvChangesCallCount++;

        if (FailNextPage)
        {
            throw new InvalidOperationException("Simulated TMDB TV changes page failure.");
        }

        if (!_windowPages.TryGetValue((startDate, endDate), out var pages))
        {
            return Task.FromResult(new TmdbTvChangesPageResult([], page, 0));
        }

        if (page < 1 || page > pages.Count)
        {
            return Task.FromResult(new TmdbTvChangesPageResult([], page, pages.Count));
        }

        return Task.FromResult(new TmdbTvChangesPageResult(
            pages[page - 1],
            page,
            pages.Count));
    }
}

public sealed class FakeTmdbTvChangesProviderAdapter(FakeTmdbTvChangesProvider inner) : ITmdbTvChangesProvider
{
    public Task<TmdbTvChangesPageResult> GetTvChangesPageAsync(
        DateOnly startDate,
        DateOnly endDate,
        int page,
        CancellationToken cancellationToken = default) =>
        inner.GetTvChangesPageAsync(startDate, endDate, page, cancellationToken);
}
