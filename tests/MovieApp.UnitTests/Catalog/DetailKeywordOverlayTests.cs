using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Catalog;

public sealed class DetailKeywordOverlayTests
{
    [Fact]
    public async Task ApplyMovieKeywordsAsync_UsesRepositoryResults()
    {
        var movieId = Guid.NewGuid();
        var keywordId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var repository = new FakeKeywordRepository([
            new CatalogKeywordSummary(keywordId, "time travel"),
            new CatalogKeywordSummary(Guid.Parse("22222222-2222-2222-2222-222222222222"), "friendship"),
        ]);
        var canonical = CreateMovie(movieId);

        var result = await DetailKeywordOverlay.ApplyMovieKeywordsAsync(
            repository,
            movieId,
            canonical,
            ContentLocaleResolver.SpanishSpain,
            CancellationToken.None);

        Assert.Equal(2, result.Keywords.Count);
        Assert.Equal(keywordId, result.Keywords[0].Id);
        Assert.Equal("time travel", result.Keywords[0].Name);
        Assert.Equal(ContentLocaleResolver.SpanishSpain, repository.LastLocale);
    }

    [Fact]
    public async Task ApplyMovieKeywordsAsync_DefaultsLocaleWhenMissing()
    {
        var movieId = Guid.NewGuid();
        var repository = new FakeKeywordRepository([]);
        var canonical = CreateMovie(movieId);

        await DetailKeywordOverlay.ApplyMovieKeywordsAsync(
            repository,
            movieId,
            canonical,
            contentLocale: null,
            CancellationToken.None);

        Assert.Equal(ContentLocaleResolver.EnglishUnitedStates, repository.LastLocale);
    }

    private static MovieDetailsResult CreateMovie(Guid id) =>
        new(
            id,
            1,
            null,
            null,
            "Title",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0m,
            0,
            [],
            [],
            null,
            true,
            false,
            false);

    private sealed class FakeKeywordRepository(IReadOnlyList<CatalogKeywordSummary> keywords)
        : ICatalogTitleKeywordReadRepository
    {
        public string? LastLocale { get; private set; }

        public Task<IReadOnlyList<CatalogKeywordSummary>> GetLocalizedKeywordsForMovieAsync(
            Guid movieId,
            string contentLocale,
            int maxCount,
            CancellationToken cancellationToken = default)
        {
            LastLocale = contentLocale;
            return Task.FromResult(keywords);
        }

        public Task<IReadOnlyList<CatalogKeywordSummary>> GetLocalizedKeywordsForTvShowAsync(
            Guid tvShowId,
            string contentLocale,
            int maxCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogKeywordSummary>>([]);
    }
}
