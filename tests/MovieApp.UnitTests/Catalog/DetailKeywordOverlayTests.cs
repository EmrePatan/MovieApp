using MovieApp.Application.Abstractions.Persistence;
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
        var repository = new FakeKeywordRepository(["time travel", "friendship"]);
        var canonical = CreateMovie(movieId);

        var result = await DetailKeywordOverlay.ApplyMovieKeywordsAsync(
            repository,
            movieId,
            canonical,
            ContentLocaleResolver.SpanishSpain,
            CancellationToken.None);

        Assert.Equal(["time travel", "friendship"], result.Keywords);
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

    private sealed class FakeKeywordRepository(IReadOnlyList<string> keywords) : ICatalogTitleKeywordReadRepository
    {
        public string? LastLocale { get; private set; }

        public Task<IReadOnlyList<string>> GetLocalizedKeywordNamesForMovieAsync(
            Guid movieId,
            string contentLocale,
            int maxCount,
            CancellationToken cancellationToken = default)
        {
            LastLocale = contentLocale;
            return Task.FromResult(keywords);
        }

        public Task<IReadOnlyList<string>> GetLocalizedKeywordNamesForTvShowAsync(
            Guid tvShowId,
            string contentLocale,
            int maxCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
