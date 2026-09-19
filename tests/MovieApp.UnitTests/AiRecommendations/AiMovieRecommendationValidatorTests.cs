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
        var validator = new AiMovieRecommendationValidator(
            resolver,
            new FakeTasteDataSource(new HashSet<Guid> { _movie2 }, new HashSet<Guid>()),
            NullAiRecommendationPerfContext.Instance);

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

        var validator = new AiMovieRecommendationValidator(
            resolver,
            new FakeTasteDataSource(new HashSet<Guid>(), new HashSet<Guid>()),
            NullAiRecommendationPerfContext.Instance);
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
            new FakeTasteDataSource(new HashSet<Guid>(), new HashSet<Guid>()),
            NullAiRecommendationPerfContext.Instance);

        var result = await validator.ValidateAsync(
            Guid.NewGuid(),
            [new AiProviderSuggestion("Missing", 2020, "movie", null, "r")],
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            5,
            CancellationToken.None);

        Assert.Equal(0, result.ValidatedCount);
        Assert.False(result.PartialResults);
    }

    [Fact]
    public async Task ValidateAsyncRecordsRejectionCounters()
    {
        var perfContext = new AiRecommendationPerfContext();
        var resolver = new FakeIdentityResolver();
        var validator = new AiMovieRecommendationValidator(
            resolver,
            new FakeTasteDataSource(new HashSet<Guid> { _movie2 }, new HashSet<Guid>()),
            perfContext);

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
            new("Old Film", 2000, "movie", null, "old"),
            new("Missing", 2020, "movie", null, "missing"),
            new("Podcast", 2020, "podcast", null, "bad-type")
        };

        resolver.SetResolver("Arrival", CreateMovie(_movie1, "Arrival", 2016, 116, ["Science Fiction"]));
        resolver.SetResolver("Watched", CreateMovie(_movie2, "Watched", 2015, 100, ["Drama"]));
        resolver.SetResolver("Duplicate", CreateMovie(_movie1, "Duplicate", 2016, 100, ["Drama"]));
        resolver.SetResolver("Session", CreateMovie(_movie3, "Session", 2018, 100, ["Drama"]));
        resolver.SetResolver("Horror Film", CreateMovie(Guid.NewGuid(), "Horror Film", 2016, 100, ["Horror"]));
        resolver.SetResolver("Long Film", CreateMovie(Guid.NewGuid(), "Long Film", 2016, 180, ["Drama"]));
        resolver.SetResolver("Old Film", CreateMovie(Guid.NewGuid(), "Old Film", 2000, 100, ["Drama"]));

        await validator.ValidateAsync(Guid.NewGuid(), suggestions, session, 5, CancellationToken.None);

        Assert.Equal(1, perfContext.Metrics.ValidationRejectedWatched);
        Assert.Equal(2, perfContext.Metrics.ValidationRejectedResponseDuplicate);
        Assert.Equal(1, perfContext.Metrics.ValidationRejectedSessionDuplicate);
        Assert.Equal(1, perfContext.Metrics.ValidationRejectedExcludedGenre);
        Assert.Equal(1, perfContext.Metrics.ValidationRejectedRuntime);
        Assert.Equal(1, perfContext.Metrics.ValidationRejectedYear);
        Assert.Equal(1, perfContext.Metrics.ValidationRejectedResolutionFailure);
        Assert.Equal(1, perfContext.Metrics.ValidationRejectedUnsupportedMediaType);
    }

    [Fact]
    public async Task ValidateAsyncAcceptsResolvedTvSuggestion()
    {
        var tvShowId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var resolver = new FakeIdentityResolver();
        resolver.SetResolver(
            "Mindhunter",
            CreateContent("tv", tvShowId, "Mindhunter", 2017, null, ["Crime", "Drama"]));

        var validator = new AiMovieRecommendationValidator(
            resolver,
            new FakeTasteDataSource(new HashSet<Guid>(), new HashSet<Guid>()),
            NullAiRecommendationPerfContext.Instance);

        var result = await validator.ValidateAsync(
            Guid.NewGuid(),
            [new AiProviderSuggestion("Mindhunter", 2017, "tv", null, "Psychological crime series")],
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            5,
            CancellationToken.None);

        Assert.Single(result.Recommendations);
        Assert.Equal("tv", result.Recommendations[0].Movie.MediaType);
    }

    [Fact]
    public async Task ValidateAsyncFiltersMixedMovieAndTvCandidatesByWatchedStatus()
    {
        var watchedMovieId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var unwatchedMovieId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var watchedTvShowId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var unwatchedTvShowId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var resolver = new FakeIdentityResolver();
        resolver.SetResolver(
            "Watched Movie",
            CreateContent("movie", watchedMovieId, "Watched Movie", 2016, 100, ["Drama"]));
        resolver.SetResolver(
            "Fresh Movie",
            CreateContent("movie", unwatchedMovieId, "Fresh Movie", 2017, 100, ["Drama"]));
        resolver.SetResolver(
            "Watched Show",
            CreateContent("tv", watchedTvShowId, "Watched Show", 2018, null, ["Crime"]));
        resolver.SetResolver(
            "Fresh Show",
            CreateContent("tv", unwatchedTvShowId, "Fresh Show", 2019, null, ["Crime"]));

        var validator = new AiMovieRecommendationValidator(
            resolver,
            new FakeTasteDataSource(
                new HashSet<Guid> { watchedMovieId },
                new HashSet<Guid> { watchedTvShowId }),
            NullAiRecommendationPerfContext.Instance);

        var result = await validator.ValidateAsync(
            Guid.NewGuid(),
            [
                new AiProviderSuggestion("Watched Movie", 2016, "movie", null, "watched movie"),
                new AiProviderSuggestion("Fresh Movie", 2017, "movie", null, "fresh movie"),
                new AiProviderSuggestion("Watched Show", 2018, "tv", null, "watched show"),
                new AiProviderSuggestion("Fresh Show", 2019, "tv", null, "fresh show"),
            ],
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            5,
            CancellationToken.None);

        Assert.Equal(2, result.ValidatedCount);
        Assert.Equal(
            ["Fresh Movie", "Fresh Show"],
            result.Recommendations.Select(recommendation => recommendation.Movie.Title).ToArray());
        Assert.Equal("movie", result.Recommendations[0].Movie.MediaType);
        Assert.Equal("tv", result.Recommendations[1].Movie.MediaType);
    }

    [Fact]
    public async Task ValidateAsyncLoadsWatchedIdsSequentially()
    {
        var resolver = new FakeIdentityResolver();
        resolver.SetResolver(
            "Fresh Movie",
            CreateContent("movie", _movie1, "Fresh Movie", 2016, 100, ["Drama"]));

        var tasteDataSource = new SequentialTasteDataSource();
        var validator = new AiMovieRecommendationValidator(
            resolver,
            tasteDataSource,
            NullAiRecommendationPerfContext.Instance);

        await validator.ValidateAsync(
            Guid.NewGuid(),
            [new AiProviderSuggestion("Fresh Movie", 2016, "movie", null, "fresh movie")],
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            5,
            CancellationToken.None);

        Assert.Equal(1, tasteDataSource.MaxConcurrentCalls);
        Assert.Equal(["GetWatchedMovieIdsAsync", "GetWatchedTvShowIdsAsync"], tasteDataSource.CallOrder);
    }

    [Fact]
    public async Task ValidateAsyncRejectsWatchedTvShow()
    {
        var tvShowId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var resolver = new FakeIdentityResolver();
        resolver.SetResolver(
            "Mindhunter",
            CreateContent("tv", tvShowId, "Mindhunter", 2017, null, ["Crime"]));

        var validator = new AiMovieRecommendationValidator(
            resolver,
            new FakeTasteDataSource(new HashSet<Guid>(), new HashSet<Guid> { tvShowId }),
            NullAiRecommendationPerfContext.Instance);

        var result = await validator.ValidateAsync(
            Guid.NewGuid(),
            [new AiProviderSuggestion("Mindhunter", 2017, "tv", null, "Psychological crime series")],
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            5,
            CancellationToken.None);

        Assert.Empty(result.Recommendations);
    }

    private static ResolvedMovieIdentity CreateMovie(
        Guid id,
        string title,
        int year,
        int runtime,
        IReadOnlyList<string> genres) =>
        CreateContent("movie", id, title, year, runtime, genres);

    private static ResolvedMovieIdentity CreateContent(
        string mediaType,
        Guid id,
        string title,
        int year,
        int? runtime,
        IReadOnlyList<string> genres) =>
        new(
            mediaType,
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

    private sealed class FakeTasteDataSource(
        IReadOnlySet<Guid> watchedMovieIds,
        IReadOnlySet<Guid>? watchedTvShowIds = null) : IAiTasteProfileDataSource
    {
        public Task<AiTasteProfileRawData> LoadAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiTasteProfileRawData([], [], [], [], []));

        public Task<IReadOnlySet<Guid>> GetWatchedMovieIdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(watchedMovieIds);

        public Task<IReadOnlySet<Guid>> GetWatchedTvShowIdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(watchedTvShowIds ?? new HashSet<Guid>());
    }

    private sealed class SequentialTasteDataSource : IAiTasteProfileDataSource
    {
        private int _activeCalls;

        public int MaxConcurrentCalls { get; private set; }

        public List<string> CallOrder { get; } = [];

        public Task<AiTasteProfileRawData> LoadAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiTasteProfileRawData([], [], [], [], []));

        public async Task<IReadOnlySet<Guid>> GetWatchedMovieIdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            await TrackCallAsync(nameof(GetWatchedMovieIdsAsync), cancellationToken);
            return new HashSet<Guid>();
        }

        public async Task<IReadOnlySet<Guid>> GetWatchedTvShowIdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            await TrackCallAsync(nameof(GetWatchedTvShowIdsAsync), cancellationToken);
            return new HashSet<Guid>();
        }

        private async Task TrackCallAsync(string methodName, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _activeCalls);
            MaxConcurrentCalls = Math.Max(MaxConcurrentCalls, active);
            CallOrder.Add(methodName);
            await Task.Delay(10, cancellationToken);
            Interlocked.Decrement(ref _activeCalls);
        }
    }
}
