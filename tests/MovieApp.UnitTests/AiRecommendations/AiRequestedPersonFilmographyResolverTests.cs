using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiRequestedPersonFilmographyResolverTests
{
    [Fact]
    public async Task TryResolveAsyncBuildsConstraintFromCombinedCredits()
    {
        var personProvider = new FakePersonProvider();
        var resolver = new AiRequestedPersonFilmographyResolver(
            personProvider,
            new AiRecommendationPerfContext());

        var constraint = await resolver.TryResolveAsync("Bensu Soral filmini öner");

        Assert.NotNull(constraint);
        Assert.Equal(1584271, constraint!.PersonTmdbId);
        Assert.Contains(new PersonFilmographyCreditKey("tv", 67750), constraint.CreditKeys);
        Assert.DoesNotContain(new PersonFilmographyCreditKey("movie", 1310830), constraint.CreditKeys);
        Assert.Equal(2, personProvider.TmdbCallCount);
    }

    [Fact]
    public async Task TryResolveAsyncReturnsNullForGenericPrompt()
    {
        var resolver = new AiRequestedPersonFilmographyResolver(
            new FakePersonProvider(),
            new AiRecommendationPerfContext());

        var constraint = await resolver.TryResolveAsync("korku filmi öner");

        Assert.Null(constraint);
    }

    private sealed class FakePersonProvider : IPersonDataProvider
    {
        public int TmdbCallCount { get; private set; }

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            TmdbCallCount++;
            return Task.FromResult(
                new PersonProviderSearchResult(
                    [new PersonProviderSummary(1584271, "Bensu Soral", null, "Acting", 20m)],
                    1,
                    pageSize,
                    1,
                    1));
        }

        public Task<PersonProviderDetails?> GetPersonAsync(int tmdbPersonId, CancellationToken cancellationToken = default)
        {
            TmdbCallCount++;
            return Task.FromResult<PersonProviderDetails?>(
                new PersonProviderDetails(
                    1584271,
                    "Bensu Soral",
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Acting",
                    [
                        new PersonFilmographyCredit("tv", 67750, "İçerde", null, "Melek", new DateOnly(2016, 9, 19), 10m, 8m),
                        new PersonFilmographyCredit("movie", 42444, "G.O.R.A.", null, "Guest", new DateOnly(2004, 1, 1), 5m, 7m)
                    ]));
        }
    }
}
