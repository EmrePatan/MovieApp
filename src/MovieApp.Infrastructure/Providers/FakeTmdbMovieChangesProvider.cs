using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeTmdbMovieChangesProvider
{
    private readonly Dictionary<(DateOnly Start, DateOnly End), List<List<int>>> _windowPages = new();

    public bool FailNextPage { get; set; }

    public int GetMovieChangesCallCount { get; private set; }

    public void ConfigureWindow(DateOnly startDate, DateOnly endDate, params int[][] pages)
    {
        _windowPages[(startDate, endDate)] = pages.Select(page => page.ToList()).ToList();
    }

    public void Reset()
    {
        _windowPages.Clear();
        FailNextPage = false;
        GetMovieChangesCallCount = 0;
    }

    public Task<TmdbChangesPageResult> GetMovieChangesPageAsync(
        DateOnly startDate,
        DateOnly endDate,
        int page,
        CancellationToken cancellationToken = default)
    {
        GetMovieChangesCallCount++;

        if (FailNextPage)
        {
            throw new InvalidOperationException("Simulated TMDB movie changes page failure.");
        }

        if (!_windowPages.TryGetValue((startDate, endDate), out var pages))
        {
            return Task.FromResult(new TmdbChangesPageResult([], page, 0));
        }

        if (page < 1 || page > pages.Count)
        {
            return Task.FromResult(new TmdbChangesPageResult([], page, pages.Count));
        }

        return Task.FromResult(new TmdbChangesPageResult(
            pages[page - 1],
            page,
            pages.Count));
    }
}

public sealed class FakeTmdbMovieChangesProviderAdapter(FakeTmdbMovieChangesProvider inner) : ITmdbMovieChangesProvider
{
    public Task<TmdbChangesPageResult> GetMovieChangesPageAsync(
        DateOnly startDate,
        DateOnly endDate,
        int page,
        CancellationToken cancellationToken = default) =>
        inner.GetMovieChangesPageAsync(startDate, endDate, page, cancellationToken);
}
