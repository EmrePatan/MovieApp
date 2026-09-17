using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiMovieRecommendationValidatorTests
{
    private readonly Guid _movie1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _movie2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _movie3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task ValidateAsyncRemovesWatchedDuplicateSessionAndConstraintViolations()
    {
        var resolver = new FakeIdentityResolver(suggestion => CreateMovie(_movie1, "Arrival", 2016, 116, ["Science Fiction"]));
        var validator = new AiMovieRecommendationValidator(resolver, new FakeTasteDataSource(new HashSet<Guid> { _movie2 }));

        var session = new AiRecommendationSessionState
        {
            SessionId = Guid.NewGuid(),
            ExcludedGenres = ["Horror"],
            MaxRuntimeMinutes = 120,
            MinYear = 2010,
            MaxYear = 2020,
            RecommendedMovieIds = { _movie3 }
        };

        var suggestions = new List<AiProviderSuggestion>
        {
            new("Arrival", 2016, "movie", null, "good"),
            new("Watched", 2015, "movie", null, "watched"),
            new("Duplicate", 2016, "movie", null, "dup"),
            new("Duplicate", 2016, "movie", null, "dup2"),
            new("Session", 2018, "movie", null, "session"),
            new("Horror Film", 2016, "movie", null, "horror"),
            new("Long Film", 2016, "movie", null, "long"),
            new("Old Film", 2000, "movie", null, "old")
        };

        resolver.SetResolver("Arrival", CreateMovie(_movie1, "Arrival", 2016, 116, ["Science Fiction"]));
        resolver.SetResolver("Watched", CreateMovie(_movie2, "Watched", 2015, 100, ["Drama"]));
        resolver.SetResolver("Duplicate", CreateMovie(_movie1, "Duplicate", 2016, 100, ["Drama"]));
        resolver.SetResolver("Session", CreateMovie(_movie3, "Session", 2018, 100, ["Drama"]));
        resolver.SetResolver("Horror Film", CreateMovie(Guid.NewGuid(), "Horror Film", 2016, 100, ["Horror"]));
        resolver.SetResolver("Long Film", CreateMovie(Guid.NewGuid(), "Long Film", 2016, 180, ["Drama"]));
        resolver.SetResolver("Old Film", CreateMovie(Guid.NewGuid(), "Old Film", 2000, 100, ["Drama"]));

        var result = await validator.ValidateAsync(Guid.NewGuid(), suggestions, session, 5, CancellationToken.None);

        Assert.Single(result.Recommendations);
        Assert.Equal("Arrival", result.Recommendations[0].Movie.Title);
        Assert.Equal(7, result.RejectedCount - 0);
        Assert.Equal(8, result.GeminiSuggestionCount);
    }

    [Fact]
    public async Task ValidateAsyncReturnsPartialWhenFewerThanMaxSurvive()
    {
        var resolver = new FakeIdentityResolver();
        resolver.SetResolver("One", CreateMovie(_movie1, "One", 2016, 100, ["Drama"]));
        resolver.SetResolver("Two", CreateMovie(_movie2, "Two", 2017, 100, ["Drama"]));
        resolver.SetResolver("Three", CreateMovie(_movie3, "Three", 2018, 100, ["Drama"]));

        var validator = new AiMovieRecommendationValidator(resolver, new FakeTasteDataSource(new HashSet<Guid>()));
        var suggestions = new[]
        {
            new AiProviderSuggestion("One", 2016, "movie", null, "r1"),
            new AiProviderSuggestion("Two", 2017, "movie", null, "r2"),
            new AiProviderSuggestion("Three", 2018, "movie", null, "r3"),
            new AiProviderSuggestion("Bad", 2019, "tv", null, "r4"),
            new AiProviderSuggestion("Missing", 2020, "movie", null, "r5")
        };

        var result = await validator.ValidateAsync(
            Guid.NewGuid(),
            suggestions,
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            5,
            CancellationToken.None);

        Assert.Equal(3, result.ValidatedCount);
        Assert.True(result.PartialResults);
    }

    [Fact]
    public async Task ValidateAsyncReturnsZeroSurvivors()
    {
        var validator = new AiMovieRecommendationValidator(
            new FakeIdentityResolver(),
            new FakeTasteDataSource(new HashSet<Guid>()));

        var result = await validator.ValidateAsync(
            Guid.NewGuid(),
            [new AiProviderSuggestion("Missing", 2020, "movie", null, "r")],
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            5,
            CancellationToken.None);

        Assert.Equal(0, result.ValidatedCount);
        Assert.False(result.PartialResults);
    }

    private static ResolvedMovieIdentity CreateMovie(
        Guid id,
        string title,
        int year,
        int runtime,
        IReadOnlyList<string> genres) =>
        new(
            id,
            100,
            title,
            year,
            runtime,
            title,
            "Overview",
            "/poster.jpg",
            null,
            new DateOnly(year, 1, 1),
            7m,
            100,
            genres);

    private sealed class FakeIdentityResolver : IMovieIdentityResolver
    {
        private readonly Dictionary<string, ResolvedMovieIdentity?> _map = new(StringComparer.OrdinalIgnoreCase);

        public FakeIdentityResolver(Func<AiProviderSuggestion, ResolvedMovieIdentity?>? defaultResolver = null)
        {
            if (defaultResolver is not null)
            {
                DefaultResolver = defaultResolver;
            }
        }

        private Func<AiProviderSuggestion, ResolvedMovieIdentity?>? DefaultResolver { get; }

        public void SetResolver(string title, ResolvedMovieIdentity? movie) => _map[title] = movie;

        public Task<ResolvedMovieIdentity?> ResolveAsync(
            AiProviderSuggestion suggestion,
            CancellationToken cancellationToken = default)
        {
            if (_map.TryGetValue(suggestion.Title, out var movie))
            {
                return Task.FromResult(movie);
            }

            return Task.FromResult(DefaultResolver?.Invoke(suggestion));
        }
    }

    private sealed class FakeTasteDataSource(IReadOnlySet<Guid> watchedMovieIds) : IAiTasteProfileDataSource
    {
        public Task<AiTasteProfileRawData> LoadAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiTasteProfileRawData([], [], [], [], []));

        public Task<IReadOnlySet<Guid>> GetWatchedMovieIdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(watchedMovieIds);
    }
}
