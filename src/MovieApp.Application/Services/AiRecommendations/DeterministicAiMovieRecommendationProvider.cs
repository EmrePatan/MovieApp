using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.AiRecommendations;

/// <summary>
/// Maps existing Movie Cave personalized/cold-start recommendation candidates into AI provider suggestions.
/// Does not parse natural-language constraint updates from the current user message.
/// </summary>
public sealed class DeterministicAiMovieRecommendationProvider(
    IRecommendationRepository recommendationRepository,
    IDiscoveryService discoveryService,
    IOptions<RecommendationOptions> recommendationOptions) : IDeterministicAiMovieRecommendationProvider
{
    public async Task<AiProviderGenerationResult> GenerateAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = recommendationOptions.Value;
        var context = await recommendationRepository.GetUserRecommendationContextAsync(
            request.UserId,
            settings.MinimumPersonalizationInteractions,
            cancellationToken);

        var excludedMovies = MergeExclusions(context.ExcludedMovieIds, request.Session.RecommendedMovieIds);
        var excludedTv = MergeExclusions(context.ExcludedTvShowIds, request.Session.RecommendedTvShowIds);

        IReadOnlyList<AiProviderSuggestion> suggestions;

        if (context.MeaningfulInteractionCount < settings.MinimumPersonalizationInteractions)
        {
            suggestions = await BuildColdStartSuggestionsAsync(request, cancellationToken);
        }
        else
        {
            suggestions = await BuildPersonalizedSuggestionsAsync(
                context,
                excludedMovies,
                excludedTv,
                request,
                settings,
                cancellationToken);
        }

        return new AiProviderGenerationResult(suggestions, ConstraintUpdates: null);
    }

    private async Task<IReadOnlyList<AiProviderSuggestion>> BuildColdStartSuggestionsAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken)
    {
        var discovery = await discoveryService.GetPopularAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, request.SuggestionCount),
            request.ResponseLanguage,
            cancellationToken);

        return discovery.Items
            .Select(item => ToSuggestion(
                item.Type,
                item.Title,
                item.Year,
                null,
                LocalizeReason(
                    RecommendationReasonBuilder.BuildColdStartPopularReason(),
                    request.ResponseLanguage)))
            .Take(request.SuggestionCount)
            .ToList();
    }

    private async Task<IReadOnlyList<AiProviderSuggestion>> BuildPersonalizedSuggestionsAsync(
        UserRecommendationContext context,
        IReadOnlySet<Guid> excludedMovies,
        IReadOnlySet<Guid> excludedTv,
        AiProviderRequest request,
        RecommendationOptions settings,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var genrePreferences = PersonalizedRecommendationEngine.BuildGenrePreferences(
            context.Signals,
            settings,
            utcNow);
        var keywordPreferences = KeywordAffinityScorer.BuildKeywordPreferences(
            context.Signals,
            settings,
            utcNow);

        var candidates = await recommendationRepository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.All,
            genrePreferences.Keys.ToList(),
            excludedMovies,
            excludedTv,
            settings.MaximumCandidates,
            cancellationToken);

        if (candidates.Count == 0)
        {
            return await BuildColdStartSuggestionsAsync(request, cancellationToken);
        }

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            candidates,
            context.Signals,
            genrePreferences,
            keywordPreferences,
            settings,
            utcNow);

        return PersonalizedRecommendationEngine.ApplyDiversity(scored, settings)
            .Select(item => ToSuggestion(
                item.Candidate.Type,
                item.Candidate.Title,
                item.Candidate.Year,
                null,
                LocalizeReason(
                    item.Reason ?? RecommendationReasonBuilder.BuildColdStartPopularReason(),
                    request.ResponseLanguage)))
            .Take(request.SuggestionCount)
            .ToList();
    }

    private static AiProviderSuggestion ToSuggestion(
        string mediaType,
        string title,
        int? year,
        int? tmdbId,
        string reason) =>
        new(
            title,
            year ?? 0,
            mediaType,
            tmdbId,
            reason);

    private static string LocalizeReason(string englishReason, string responseLanguage) =>
        RecommendationReasonLocalization.Localize(englishReason, responseLanguage);

    private static HashSet<Guid> MergeExclusions(IReadOnlySet<Guid> baseSet, HashSet<Guid> sessionIds)
    {
        var merged = new HashSet<Guid>(baseSet);
        foreach (var id in sessionIds)
        {
            merged.Add(id);
        }

        return merged;
    }
}
