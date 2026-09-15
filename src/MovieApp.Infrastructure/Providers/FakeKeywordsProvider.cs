using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeKeywordsProvider : IKeywordsProvider
{
    public static readonly IReadOnlyList<ProviderKeywordSummary> InterstellarKeywords =
    [
        new(4379, "space travel"),
        new(9663, "time travel"),
    ];

    public static readonly IReadOnlyList<ProviderKeywordSummary> BreakingBadKeywords =
    [
        new(9715, "drug dealer"),
        new(4379, "space travel"),
    ];

    public Task<IReadOnlyList<ProviderKeywordSummary>> GetMovieKeywordsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProviderKeywordSummary>>(
            tmdbId == FakeMovieDataProvider.InterstellarTmdbId ? InterstellarKeywords : []);

    public Task<IReadOnlyList<ProviderKeywordSummary>> GetTvShowKeywordsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProviderKeywordSummary>>(
            tmdbId == FakeTvShowDataProvider.BreakingBadTmdbId ? BreakingBadKeywords : []);
}
