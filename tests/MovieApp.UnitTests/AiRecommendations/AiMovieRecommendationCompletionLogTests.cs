using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiMovieRecommendationCompletionLogTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task CompletionLogReflectsExternalProviderAndPostValidationCount()
    {
        var logger = new CapturingLogger<AiMovieRecommendationService>();
        var service = CreateService(
            logger,
            new FakeProvider(selectedSource: "Groq", isAiGenerated: true),
            new FakeValidator(
                new AiValidationResult(
                    [CreateValidatedRecommendation()],
                    10,
                    2,
                    8,
                    true)));

        var result = await service.GetRecommendationsAsync(UserId, "mystery movie", null, "en-US");

        Assert.Equal(2, result.ReturnedCount);
        var entry = Assert.Single(logger.Entries, entry => entry.EventId.Id == 7203);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Success", entry.Properties["Outcome"]);
        Assert.Equal("Groq", entry.Properties["SelectedSource"]);
        Assert.Equal(true, entry.Properties["IsAiGenerated"]);
        Assert.Equal(2, entry.Properties["ReturnedCount"]);
    }

    [Fact]
    public async Task CompletionLogReflectsDeterministicFallback()
    {
        var logger = new CapturingLogger<AiMovieRecommendationService>();
        var service = CreateService(
            logger,
            new FakeProvider(selectedSource: "deterministic", isAiGenerated: false),
            new FakeValidator(
                new AiValidationResult(
                    [CreateValidatedRecommendation()],
                    5,
                    1,
                    4,
                    false)));

        await service.GetRecommendationsAsync(UserId, "mystery movie", null, "en-US");

        var entry = Assert.Single(logger.Entries, entry => entry.EventId.Id == 7203);
        Assert.Equal("deterministic", entry.Properties["SelectedSource"]);
        Assert.Equal(false, entry.Properties["IsAiGenerated"]);
        Assert.Equal(1, entry.Properties["ReturnedCount"]);
    }

    [Fact]
    public async Task CompletionLogUsesNoValidResultsOutcomeWhenValidationReturnsZero()
    {
        var logger = new CapturingLogger<AiMovieRecommendationService>();
        var service = CreateService(
            logger,
            new FakeProvider(selectedSource: "deterministic", isAiGenerated: false),
            new FakeValidator(new AiValidationResult([], 8, 0, 8, false)));

        var result = await service.GetRecommendationsAsync(UserId, "mystery movie", null, "en-US");

        Assert.Equal(0, result.ReturnedCount);
        var entry = Assert.Single(logger.Entries, entry => entry.EventId.Id == 7203);
        Assert.Equal("NoValidResults", entry.Properties["Outcome"]);
        Assert.Equal(0, entry.Properties["ReturnedCount"]);
    }

    [Fact]
    public async Task ProviderFailureDoesNotEmitCompletionLog()
    {
        var logger = new CapturingLogger<AiMovieRecommendationService>();
        var service = CreateService(
            logger,
            new FakeProvider(shouldFail: true),
            new FakeValidator(new AiValidationResult([], 0, 0, 0, false)));

        await Assert.ThrowsAsync<AiRecommendationProviderUnavailableException>(() =>
            service.GetRecommendationsAsync(UserId, "mystery movie", null, "en-US"));

        Assert.DoesNotContain(logger.Entries, entry => entry.EventId.Id == 7203);
    }

    private static AiMovieRecommendationService CreateService(
        CapturingLogger<AiMovieRecommendationService> logger,
        FakeProvider provider,
        FakeValidator validator) =>
        new(
            new FakeEntitlementService(),
            new FakeQuotaService(),
            new FakeTasteProfileBuilder(),
            new FakeSessionStore(),
            provider,
            validator,
            Options.Create(new AiRecommendationOptions
            {
                SuggestionCount = 10,
                MaxReturnedCount = 5,
                UserDailyMessageLimit = 3
            }),
            NullAiRecommendationPerfContext.Instance,
            logger);

    private static AiValidatedRecommendation CreateValidatedRecommendation() =>
        new(
            new ResolvedMovieIdentity(
                "movie",
                Guid.NewGuid(),
                1,
                "Arrival",
                2016,
                116,
                "Arrival",
                "Overview",
                null,
                null,
                new DateOnly(2016, 1, 1),
                8m,
                100,
                ["Science Fiction"]),
            "Reason");

    private sealed class FakeEntitlementService : IAiRecommendationEntitlementService
    {
        public Task EnsurePremiumEntitledAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeQuotaService : IAiRecommendationQuotaService
    {
        public Task<AiQuotaReservation> CheckAndReserveAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiQuotaReservation("reservation"));

        public Task CommitAsync(Guid userId, AiQuotaReservation reservation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReleaseAsync(Guid userId, AiQuotaReservation reservation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<int> GetRemainingUserQuotaAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(2);
    }

    private sealed class FakeTasteProfileBuilder : IAiTasteProfileBuilder
    {
        public Task<AiTasteProfile> BuildAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiTasteProfile([], [], [], [], [], [], [], [], true));
    }

    private sealed class FakeSessionStore : IAiRecommendationSessionStore
    {
        public Task<AiRecommendationSessionState?> GetAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AiRecommendationSessionState?>(null);

        public Task SaveAsync(
            Guid userId,
            AiRecommendationSessionState session,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeProvider(
        bool shouldFail = false,
        bool isAiGenerated = true,
        string selectedSource = "fake") : IAiMovieRecommendationProvider
    {
        public Task<AiMovieRecommendationProviderOutcome> GenerateAsync(
            AiProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                throw new AiRecommendationProviderException("Provider failed.");
            }

            return Task.FromResult(new AiMovieRecommendationProviderOutcome(
                new AiProviderGenerationResult(
                    [new AiProviderSuggestion("Arrival", 2016, "movie", 1, "Reason")],
                    null),
                isAiGenerated,
                selectedSource));
        }
    }

    private sealed class FakeValidator(AiValidationResult result) : IAiMovieRecommendationValidator
    {
        public Task<AiValidationResult> ValidateAsync(
            Guid userId,
            IReadOnlyList<AiProviderSuggestion> suggestions,
            AiRecommendationSessionState session,
            int maxReturnedCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>();
            if (state is IReadOnlyList<KeyValuePair<string, object?>> structuredState)
            {
                foreach (var pair in structuredState)
                {
                    properties[pair.Key] = pair.Value;
                }
            }

            Entries.Add(new LogEntry(logLevel, eventId, properties, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        EventId EventId,
        IReadOnlyDictionary<string, object?> Properties,
        string Message);
}
