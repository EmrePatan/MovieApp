using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class FailoverAiMovieRecommendationProvider(
    IEnumerable<IAiExternalLlmRecommendationProvider> providers,
    IDeterministicAiMovieRecommendationProvider deterministicProvider,
    IOptions<AiRecommendationOptions> options,
    IAiRecommendationPerfContext perfContext,
    ILogger<FailoverAiMovieRecommendationProvider> logger) : IAiMovieRecommendationProvider
{
    private readonly IReadOnlyList<IAiExternalLlmRecommendationProvider> _providers = providers.ToList();

    public async Task<AiMovieRecommendationProviderOutcome> GenerateAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var chainBudget = TimeSpan.FromSeconds(Math.Max(1, settings.ExternalLlmChainBudgetSeconds));
        var chainStopwatch = Stopwatch.StartNew();
        var providerAttempts = 0;

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!provider.IsConfigured(settings))
            {
                perfContext.RecordProviderSkipped(provider.ProviderName, AiProviderFailureCategory.NotConfigured);
                FailoverAiMovieRecommendationProviderLogMessages.LogProviderSkipped(
                    logger,
                    provider.ProviderName,
                    AiProviderFailureCategory.NotConfigured,
                    null);
                continue;
            }

            var remainingBudget = chainBudget - chainStopwatch.Elapsed;
            if (remainingBudget <= TimeSpan.Zero)
            {
                perfContext.RecordLlmChainBudgetExhausted();
                break;
            }

            var configuredTimeout = ResolveProviderTimeoutSeconds(provider.ProviderName, settings);
            var attemptTimeout = TimeSpan.FromSeconds(
                Math.Min(configuredTimeout, Math.Max(1, (int)Math.Ceiling(remainingBudget.TotalSeconds))));

            providerAttempts++;
            var attempt = await provider.TryGenerateAsync(request, attemptTimeout, cancellationToken);
            perfContext.RecordProviderAttempt(attempt);

            if (attempt.Succeeded && attempt.Result is not null)
            {
                chainStopwatch.Stop();
                perfContext.RecordLlmChainSuccess(
                    provider.ProviderName,
                    chainStopwatch.ElapsedMilliseconds,
                    attempt.DurationMs,
                    providerAttempts);
                return new AiMovieRecommendationProviderOutcome(attempt.Result, true, provider.ProviderName);
            }

            FailoverAiMovieRecommendationProviderLogMessages.LogProviderSkipped(
                logger,
                provider.ProviderName,
                attempt.FailureCategory ?? AiProviderFailureCategory.HttpError,
                attempt.HttpStatusCode);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var deterministicStopwatch = Stopwatch.StartNew();
        var deterministic = await deterministicProvider.GenerateAsync(request, cancellationToken);
        deterministicStopwatch.Stop();
        chainStopwatch.Stop();

        perfContext.RecordDeterministicFallback(
            deterministicStopwatch.ElapsedMilliseconds,
            chainStopwatch.ElapsedMilliseconds,
            providerAttempts);

        if (deterministic.Suggestions.Count == 0)
        {
            throw new AiRecommendationProviderException("All recommendation providers failed.");
        }

        return new AiMovieRecommendationProviderOutcome(deterministic, false, "deterministic");
    }

    private static int ResolveProviderTimeoutSeconds(string providerName, AiRecommendationOptions settings) =>
        providerName switch
        {
            "Gemini" => settings.ResolveProviderTimeoutSeconds(settings.Gemini.RequestTimeoutSeconds),
            "Groq" => settings.ResolveProviderTimeoutSeconds(settings.Groq.RequestTimeoutSeconds),
            "OpenRouter" => settings.ResolveProviderTimeoutSeconds(settings.OpenRouter.RequestTimeoutSeconds),
            "Cloudflare" => settings.ResolveProviderTimeoutSeconds(settings.Cloudflare.RequestTimeoutSeconds),
            _ => settings.DefaultProviderRequestTimeoutSeconds
        };
}

internal static partial class FailoverAiMovieRecommendationProviderLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "AI recommendation provider {ProviderName} skipped. Category={FailureCategory} HttpStatus={HttpStatusCode}")]
    public static partial void LogProviderSkipped(
        ILogger logger,
        string providerName,
        AiProviderFailureCategory failureCategory,
        int? httpStatusCode);
}
