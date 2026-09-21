using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiMovieRecommendationServiceTests
{
    private readonly Guid _userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetRecommendationsAsyncSucceedsWithoutPremiumEntitlement()
    {
        var service = CreateService(
            new FakeEntitlementService(),
            new FakeQuotaService(),
            new FakeProvider(),
            new FakeValidator(
                new AiValidationResult(
                    [
                        new AiValidatedRecommendation(
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
                            "Mind-bending")
                    ],
                    1,
                    1,
                    0,
                    false)));

        var result = await service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US");

        Assert.Equal(1, result.ReturnedCount);
    }

    [Fact]
    public async Task GetRecommendationsAsyncThrowsWhenQuotaExceeded()
    {
        var service = CreateService(
            new FakeEntitlementService(),
            new FakeQuotaService(shouldAllowReserve: false),
            new FakeProvider(),
            new FakeValidator());

        await Assert.ThrowsAsync<AiRecommendationQuotaExceededException>(() =>
            service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US"));
    }

    [Fact]
    public async Task GetRecommendationsAsyncReturns503WhenProviderFails()
    {
        var service = CreateService(
            new FakeEntitlementService(),
            new FakeQuotaService(),
            new FakeProvider(shouldFail: true),
            new FakeValidator());

        await Assert.ThrowsAsync<AiRecommendationProviderUnavailableException>(() =>
            service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US"));
    }

    [Fact]
    public async Task GetRecommendationsAsyncReturnsSuccessfulResult()
    {
        var movieId = Guid.NewGuid();
        var service = CreateService(
            new FakeEntitlementService(),
            new FakeQuotaService(),
            new FakeProvider(),
            new FakeValidator(
                new AiValidationResult(
                    [
                        new AiValidatedRecommendation(
                            new ResolvedMovieIdentity(
                                "movie",
                                movieId,
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
                            "Mind-bending")
                    ],
                    1,
                    1,
                    0,
                    false)));

        var result = await service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US");

        Assert.True(result.IsAiGenerated);
        Assert.Equal(1, result.ReturnedCount);
        Assert.Equal(2, result.QuotaRemaining);
    }

    [Fact]
    public async Task GetRecommendationsAsyncPassesConfiguredSuggestionCountToProvider()
    {
        var provider = new FakeProvider();
        var service = CreateService(
            new FakeEntitlementService(),
            new FakeQuotaService(),
            provider,
            new FakeValidator());

        await service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US");

        Assert.Equal(10, provider.LastSuggestionCount);
    }

    [Fact]
    public async Task GetRecommendationsAsyncCommitsQuotaOnlyAfterValidationSucceeds()
    {
        var quota = new TrackingQuotaService();
        var service = CreateService(
            new FakeEntitlementService(),
            quota,
            new FakeProvider(),
            new FakeValidator(
                new AiValidationResult(
                    [
                        new AiValidatedRecommendation(
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
                            "Mind-bending")
                    ],
                    10,
                    1,
                    9,
                    false)));

        await service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US");

        Assert.Equal(1, quota.CommitCount);
        Assert.Equal(0, quota.ReleaseCount);
    }

    [Fact]
    public async Task GetRecommendationsAsyncReleasesQuotaWhenValidationFails()
    {
        var quota = new TrackingQuotaService();
        var service = CreateService(
            new FakeEntitlementService(),
            quota,
            new FakeProvider(),
            new ThrowingValidator());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US"));

        Assert.Equal(0, quota.CommitCount);
        Assert.Equal(1, quota.ReleaseCount);
    }

    [Fact]
    public async Task GetRecommendationsAsyncReleasesQuotaWhenProviderFails()
    {
        var quota = new TrackingQuotaService();
        var service = CreateService(
            new FakeEntitlementService(),
            quota,
            new FakeProvider(shouldFail: true),
            new FakeValidator());

        await Assert.ThrowsAsync<AiRecommendationProviderUnavailableException>(() =>
            service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US"));

        Assert.Equal(0, quota.CommitCount);
        Assert.Equal(1, quota.ReleaseCount);
    }

    [Fact]
    public async Task GetRecommendationsAsyncReturnsZeroValidatedWithoutSecondProviderCall()
    {
        var provider = new FakeProvider();
        var service = CreateService(
            new FakeEntitlementService(),
            new FakeQuotaService(),
            provider,
            new FakeValidator(
                new AiValidationResult([], 8, 0, 8, false)));

        var result = await service.GetRecommendationsAsync(_userId, "mystery movie", null, "en-US");

        Assert.Equal(0, result.ReturnedCount);
        Assert.Equal(1, provider.CallCount);
        Assert.True(result.IsAiGenerated);
    }

    private static AiMovieRecommendationService CreateService(
        IAiRecommendationEntitlementService entitlement,
        IAiRecommendationQuotaService quota,
        IAiMovieRecommendationProvider provider,
        IAiMovieRecommendationValidator validator) =>
        new(
            entitlement,
            quota,
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
            NullLogger<AiMovieRecommendationService>.Instance);

    private sealed class FakeEntitlementService(bool shouldAllow = true) : IAiRecommendationEntitlementService
    {
        public Task EnsurePremiumEntitledAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (!shouldAllow)
            {
                throw new AiRecommendationEntitlementException("Premium required.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeQuotaService(bool shouldAllowReserve = true) : IAiRecommendationQuotaService
    {
        private int _committed;

        public Task<AiQuotaReservation> CheckAndReserveAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (!shouldAllowReserve)
            {
                throw new AiRecommendationQuotaExceededException("Quota exceeded.");
            }

            return Task.FromResult(new AiQuotaReservation("reservation"));
        }

        public Task CommitAsync(Guid userId, AiQuotaReservation reservation, CancellationToken cancellationToken = default)
        {
            _committed++;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(Guid userId, AiQuotaReservation reservation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<int> GetRemainingUserQuotaAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Math.Max(0, 3 - _committed));
    }

    private sealed class TrackingQuotaService : IAiRecommendationQuotaService
    {
        public int CommitCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public Task<AiQuotaReservation> CheckAndReserveAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiQuotaReservation("reservation"));

        public Task CommitAsync(Guid userId, AiQuotaReservation reservation, CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(Guid userId, AiQuotaReservation reservation, CancellationToken cancellationToken = default)
        {
            ReleaseCount++;
            return Task.CompletedTask;
        }

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

    private sealed class FakeProvider(bool shouldFail = false) : IAiMovieRecommendationProvider
    {
        public int CallCount { get; private set; }

        public int LastSuggestionCount { get; private set; }

        public Task<AiProviderGenerationResult> GenerateAsync(
            AiProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSuggestionCount = request.SuggestionCount;
            if (shouldFail)
            {
                throw new AiRecommendationProviderException("Provider failed.");
            }

            return Task.FromResult(new AiProviderGenerationResult(
                [new AiProviderSuggestion("Arrival", 2016, "movie", 1, "Reason")],
                null));
        }
    }

    private sealed class FakeValidator(AiValidationResult? result = null) : IAiMovieRecommendationValidator
    {
        public Task<AiValidationResult> ValidateAsync(
            Guid userId,
            IReadOnlyList<AiProviderSuggestion> suggestions,
            AiRecommendationSessionState session,
            int maxReturnedCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result ?? new AiValidationResult([], suggestions.Count, 0, suggestions.Count, false));
    }

    private sealed class ThrowingValidator : IAiMovieRecommendationValidator
    {
        public Task<AiValidationResult> ValidateAsync(
            Guid userId,
            IReadOnlyList<AiProviderSuggestion> suggestions,
            AiRecommendationSessionState session,
            int maxReturnedCount,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Validation failed.");
    }
}
