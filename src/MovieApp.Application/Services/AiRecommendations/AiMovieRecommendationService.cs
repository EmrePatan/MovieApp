using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using Microsoft.Extensions.Options;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class AiMovieRecommendationService(
    IAiRecommendationEntitlementService entitlementService,
    IAiRecommendationQuotaService quotaService,
    IAiTasteProfileBuilder tasteProfileBuilder,
    IAiRecommendationSessionStore sessionStore,
    IAiMovieRecommendationProvider provider,
    IAiMovieRecommendationValidator validator,
    IOptions<AiRecommendationOptions> options,
    IAiRecommendationPerfContext perfContext,
    ILogger<AiMovieRecommendationService> logger) : IAiMovieRecommendationService
{
    public async Task<AiRecommendationServiceResult> GetRecommendationsAsync(
        Guid userId,
        string message,
        Guid? sessionId,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var settings = options.Value;

        try
        {
            await entitlementService.EnsurePremiumEntitledAsync(userId, cancellationToken);

            var quotaReserveStopwatch = Stopwatch.StartNew();
            var reservation = await quotaService.CheckAndReserveAsync(userId, cancellationToken);
            quotaReserveStopwatch.Stop();
            perfContext.RecordQuotaReserveMs(quotaReserveStopwatch.ElapsedMilliseconds);

            var quotaCommitted = false;

            try
            {
                var tasteProfile = await tasteProfileBuilder.BuildAsync(userId, cancellationToken);

                var sessionLoadStopwatch = Stopwatch.StartNew();
                var session = await LoadOrCreateSessionAsync(userId, sessionId, message, cancellationToken);
                sessionLoadStopwatch.Stop();
                perfContext.RecordSessionLoadMs(sessionLoadStopwatch.ElapsedMilliseconds);

                var providerRequest = new AiProviderRequest(
                    message,
                    tasteProfile,
                    session,
                    settings.SuggestionCount);

                AiProviderGenerationResult generation;
                try
                {
                    generation = await provider.GenerateAsync(providerRequest, cancellationToken);
                }
                catch (AiRecommendationProviderException)
                {
                    await quotaService.ReleaseAsync(userId, reservation, cancellationToken);
                    perfContext.SetOutcome("ProviderUnavailable");
                    throw new AiRecommendationProviderUnavailableException(
                        "AI recommendation provider is temporarily unavailable.");
                }

                var quotaCommitStopwatch = Stopwatch.StartNew();
                await quotaService.CommitAsync(userId, reservation, cancellationToken);
                quotaCommitStopwatch.Stop();
                perfContext.RecordQuotaCommitMs(quotaCommitStopwatch.ElapsedMilliseconds);
                quotaCommitted = true;

                AiSessionConstraintMerger.Merge(session, generation.ConstraintUpdates);

                var validation = await validator.ValidateAsync(
                    userId,
                    generation.Suggestions,
                    session,
                    settings.MaxReturnedCount,
                    cancellationToken);

                UpdateSessionAfterValidation(session, message, validation.Recommendations);

                var sessionSaveStopwatch = Stopwatch.StartNew();
                await sessionStore.SaveAsync(userId, session, cancellationToken);
                sessionSaveStopwatch.Stop();
                perfContext.RecordSessionSaveMs(sessionSaveStopwatch.ElapsedMilliseconds);

                var quotaRemaining = await quotaService.GetRemainingUserQuotaAsync(userId, cancellationToken);

                perfContext.SetOutcome("Success");
                perfContext.SetResultCounts(validation.GeminiSuggestionCount, validation.ValidatedCount);

                return new AiRecommendationServiceResult(
                    session.SessionId,
                    IsAiGenerated: true,
                    validation.PartialResults,
                    settings.MaxReturnedCount,
                    validation.ValidatedCount,
                    quotaRemaining,
                    validation.Recommendations,
                    validation);
            }
            catch (AiRecommendationQuotaExceededException)
            {
                perfContext.SetOutcome("QuotaExceeded");
                throw;
            }
            catch (AiRecommendationEntitlementException)
            {
                perfContext.SetOutcome("EntitlementDenied");
                throw;
            }
            catch (AiRecommendationNoValidResultsException)
            {
                perfContext.SetOutcome("NoValidResults");
                throw;
            }
            catch (AiRecommendationProviderUnavailableException)
            {
                throw;
            }
            catch (AiRecommendationInfrastructureUnavailableException)
            {
                perfContext.SetOutcome("InfrastructureUnavailable");
                throw;
            }
            catch
            {
                perfContext.SetOutcome("Error");
                if (!quotaCommitted)
                {
                    await quotaService.ReleaseAsync(userId, reservation, cancellationToken);
                }

                throw;
            }
        }
        catch (AiRecommendationQuotaExceededException)
        {
            perfContext.SetOutcome("QuotaExceeded");
            throw;
        }
        catch (AiRecommendationEntitlementException)
        {
            perfContext.SetOutcome("EntitlementDenied");
            throw;
        }
        finally
        {
            totalStopwatch.Stop();
            perfContext.Metrics.TotalMs = totalStopwatch.ElapsedMilliseconds;
            AiRecommendationPerfLogMessages.LogRequest(logger, perfContext.Metrics);
        }
    }

    private async Task<AiRecommendationSessionState> LoadOrCreateSessionAsync(
        Guid userId,
        Guid? sessionId,
        string message,
        CancellationToken cancellationToken)
    {
        if (sessionId.HasValue)
        {
            var existing = await sessionStore.GetAsync(userId, sessionId.Value, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        return new AiRecommendationSessionState
        {
            SessionId = sessionId ?? Guid.NewGuid()
        };
    }

    private static void UpdateSessionAfterValidation(
        AiRecommendationSessionState session,
        string message,
        IReadOnlyList<AiValidatedRecommendation> recommendations)
    {
        session.TurnCount++;
        session.LastUserMessage = message;

        foreach (var recommendation in recommendations)
        {
            session.RecommendedMovieIds.Add(recommendation.Movie.MovieId);
            if (recommendation.Movie.TmdbId.HasValue)
            {
                session.RecommendedTmdbIds.Add(recommendation.Movie.TmdbId.Value);
            }
        }
    }
}
