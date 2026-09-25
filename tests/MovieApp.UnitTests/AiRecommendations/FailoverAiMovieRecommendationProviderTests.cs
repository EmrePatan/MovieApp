using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Services.AiRecommendations;
using MovieApp.Infrastructure.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class FailoverAiMovieRecommendationProviderTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GeminiSuccessDoesNotCallOtherProviders()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Success);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq], deterministicSucceeds: true);

        await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal(1, gemini.CallCount);
        Assert.Equal(0, groq.CallCount);
    }

    [Fact]
    public async Task Gemini503FallsThroughToGroqSuccess()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Http503);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal("Groq", outcome.SelectedSource);
        Assert.True(outcome.IsAiGenerated);
        Assert.Equal(1, gemini.CallCount);
        Assert.Equal(1, groq.CallCount);
    }

    [Fact]
    public async Task GeminiTimeoutFallsThroughToGroqSuccess()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Timeout);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal("Groq", outcome.SelectedSource);
        Assert.Equal(1, gemini.CallCount);
        Assert.Equal(1, groq.CallCount);
    }

    [Fact]
    public async Task Gemini429FallsThroughToGroqSuccess()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Http429);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal("Groq", outcome.SelectedSource);
    }

    [Fact]
    public async Task GeminiMalformedFallsThroughToGroqSuccess()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Malformed);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal("Groq", outcome.SelectedSource);
    }

    [Fact]
    public async Task GeminiAndGroqFailOpenRouterSucceeds()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Http503);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Http503);
        var openRouter = new StubExternalProvider("OpenRouter", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq, openRouter], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal("OpenRouter", outcome.SelectedSource);
        Assert.Equal(1, openRouter.CallCount);
    }

    [Fact]
    public async Task FirstThreeFailCloudflareSucceeds()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Http503);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Http503);
        var openRouter = new StubExternalProvider("OpenRouter", configured: true, behavior: ProviderBehavior.Http503);
        var cloudflare = new StubExternalProvider("Cloudflare", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq, openRouter, cloudflare], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal("Cloudflare", outcome.SelectedSource);
        Assert.Equal(1, cloudflare.CallCount);
    }

    [Fact]
    public async Task AllExternalProvidersFailUsesDeterministicFallback()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Http503);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Http503);
        var deterministic = new TrackingDeterministicProvider(succeeds: true);
        var orchestrator = CreateOrchestrator([gemini, groq], deterministic);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.False(outcome.IsAiGenerated);
        Assert.Equal("deterministic", outcome.SelectedSource);
        Assert.Equal(1, deterministic.CallCount);
    }

    [Fact]
    public async Task EachExternalProviderCalledAtMostOnce()
    {
        var providers = new[]
        {
            new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Http503),
            new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Http503),
            new StubExternalProvider("OpenRouter", configured: true, behavior: ProviderBehavior.Http503),
            new StubExternalProvider("Cloudflare", configured: true, behavior: ProviderBehavior.Http503)
        };

        var orchestrator = CreateOrchestrator(providers, deterministicSucceeds: true);
        await orchestrator.GenerateAsync(CreateRequest());

        Assert.All(providers, provider => Assert.Equal(1, provider.CallCount));
    }

    [Fact]
    public async Task DisabledProviderPerformsZeroHttpCalls()
    {
        var gemini = new StubExternalProvider("Gemini", configured: false, behavior: ProviderBehavior.Success);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq], deterministicSucceeds: true);

        await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal(0, gemini.CallCount);
        Assert.Equal(1, groq.CallCount);
    }

    [Fact]
    public async Task CallerCancellationStopsChain()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Delay);
        var orchestrator = CreateOrchestrator([gemini], deterministicSucceeds: true);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            orchestrator.GenerateAsync(CreateRequest(), cts.Token));
    }

    [Fact]
    public async Task InternalProviderTimeoutContinuesChain()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Timeout);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini, groq], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.Equal("Groq", outcome.SelectedSource);
    }

    [Fact]
    public async Task GlobalLlmBudgetExhaustionUsesDeterministicFallback()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Delay);
        var groq = new StubExternalProvider("Groq", configured: true, behavior: ProviderBehavior.Success);
        var options = CreateOptions(externalBudgetSeconds: 1);
        var orchestrator = new FailoverAiMovieRecommendationProvider(
            [gemini, groq],
            new TrackingDeterministicProvider(true),
            Options.Create(options),
            new AiRecommendationPerfContext(),
            NullLogger<FailoverAiMovieRecommendationProvider>.Instance);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.False(outcome.IsAiGenerated);
        Assert.Equal(0, groq.CallCount);
    }

    [Fact]
    public async Task ExternalProviderSetsIsAiGeneratedTrue()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Success);
        var orchestrator = CreateOrchestrator([gemini], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.True(outcome.IsAiGenerated);
    }

    [Fact]
    public async Task DeterministicSetsIsAiGeneratedFalse()
    {
        var gemini = new StubExternalProvider("Gemini", configured: true, behavior: ProviderBehavior.Http503);
        var orchestrator = CreateOrchestrator([gemini], deterministicSucceeds: true);

        var outcome = await orchestrator.GenerateAsync(CreateRequest());

        Assert.False(outcome.IsAiGenerated);
    }

    private static FailoverAiMovieRecommendationProvider CreateOrchestrator(
        IReadOnlyList<IAiExternalLlmRecommendationProvider> providers,
        bool deterministicSucceeds) =>
        CreateOrchestrator(providers, new TrackingDeterministicProvider(deterministicSucceeds));

    private static FailoverAiMovieRecommendationProvider CreateOrchestrator(
        IReadOnlyList<IAiExternalLlmRecommendationProvider> providers,
        IDeterministicAiMovieRecommendationProvider deterministic) =>
        new(
            providers,
            deterministic,
            Options.Create(CreateOptions()),
            new AiRecommendationPerfContext(),
            NullLogger<FailoverAiMovieRecommendationProvider>.Instance);

    private static AiRecommendationOptions CreateOptions(int externalBudgetSeconds = 20) =>
        new()
        {
            ExternalLlmChainBudgetSeconds = externalBudgetSeconds,
            DefaultProviderRequestTimeoutSeconds = 1,
            Gemini = new GeminiAiRecommendationOptions { RequestTimeoutSeconds = 1 }
        };

    private static AiProviderRequest CreateRequest() =>
        new(
            UserId,
            "mystery movie",
            new AiTasteProfile([], [], [], [], [], [], [], [], true),
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            5,
            "en-US");

    private enum ProviderBehavior
    {
        Success,
        Http503,
        Http429,
        Malformed,
        Timeout,
        Delay
    }

    private sealed class StubExternalProvider(
        string providerName,
        bool configured,
        ProviderBehavior behavior) : IAiExternalLlmRecommendationProvider
    {
        public int CallCount { get; private set; }

        public string ProviderName => providerName;

        public bool IsConfigured(AiRecommendationOptions options) => configured;

        public async Task<AiExternalLlmProviderAttempt> TryGenerateAsync(
            AiProviderRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            CallCount++;

            return behavior switch
            {
                ProviderBehavior.Success => Success(),
                ProviderBehavior.Http503 => Failure(AiProviderFailureCategory.HttpError, 503),
                ProviderBehavior.Http429 => Failure(AiProviderFailureCategory.HttpError, 429),
                ProviderBehavior.Malformed => Failure(AiProviderFailureCategory.MalformedResponse, null),
                ProviderBehavior.Timeout => await ExternalLlmRecommendationAttemptExecutor.ExecuteAsync(
                    ProviderName,
                    _ => Task.FromException<AiProviderGenerationResult>(new OperationCanceledException()),
                    TimeSpan.FromMilliseconds(1),
                    CancellationToken.None),
                ProviderBehavior.Delay => await ExternalLlmRecommendationAttemptExecutor.ExecuteAsync(
                    ProviderName,
                    async token =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), token);
                        return CreateResult();
                    },
                    timeout,
                    cancellationToken),
                _ => Success()
            };
        }

        private static AiExternalLlmProviderAttempt Success() =>
            new("provider", true, CreateResult(), null, null, 1);

        private static AiExternalLlmProviderAttempt Failure(AiProviderFailureCategory category, int? status) =>
            new("provider", false, null, category, status, 1);

        private static AiProviderGenerationResult CreateResult() =>
            new(
                [new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Reason")],
                null);
    }

    private sealed class TrackingDeterministicProvider(bool succeeds) : IDeterministicAiMovieRecommendationProvider
    {
        public int CallCount { get; private set; }

        public Task<AiProviderGenerationResult> GenerateAsync(
            AiProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (!succeeds)
            {
                return Task.FromResult(new AiProviderGenerationResult([], null));
            }

            return Task.FromResult(new AiProviderGenerationResult(
                [new AiProviderSuggestion("Arrival", 2016, "movie", 329996, "Popular right now")],
                null));
        }
    }
}
