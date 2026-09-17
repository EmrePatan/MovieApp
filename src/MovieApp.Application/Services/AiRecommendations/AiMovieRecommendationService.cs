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
    IOptions<AiRecommendationOptions> options) : IAiMovieRecommendationService
{
    public async Task<AiRecommendationServiceResult> GetRecommendationsAsync(
        Guid userId,
        string message,
        Guid? sessionId,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        await entitlementService.EnsurePremiumEntitledAsync(userId, cancellationToken);

        var reservation = await quotaService.CheckAndReserveAsync(userId, cancellationToken);
        var quotaCommitted = false;

        try
        {
            var tasteProfile = await tasteProfileBuilder.BuildAsync(userId, cancellationToken);
            var session = await LoadOrCreateSessionAsync(userId, sessionId, message, cancellationToken);

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
                throw new AiRecommendationProviderUnavailableException(
                    "AI recommendation provider is temporarily unavailable.");
            }

            await quotaService.CommitAsync(userId, reservation, cancellationToken);
            quotaCommitted = true;

            AiSessionConstraintMerger.Merge(session, generation.ConstraintUpdates);

            var validation = await validator.ValidateAsync(
                userId,
                generation.Suggestions,
                session,
                settings.MaxReturnedCount,
                cancellationToken);

            UpdateSessionAfterValidation(session, message, validation.Recommendations);
            await sessionStore.SaveAsync(userId, session, cancellationToken);

            var quotaRemaining = await quotaService.GetRemainingUserQuotaAsync(userId, cancellationToken);

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
            throw;
        }
        catch (AiRecommendationEntitlementException)
        {
            throw;
        }
        catch (AiRecommendationNoValidResultsException)
        {
            throw;
        }
        catch (AiRecommendationProviderUnavailableException)
        {
            throw;
        }
        catch (AiRecommendationInfrastructureUnavailableException)
        {
            throw;
        }
        catch
        {
            if (!quotaCommitted)
            {
                await quotaService.ReleaseAsync(userId, reservation, cancellationToken);
            }

            throw;
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
