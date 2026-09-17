using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class AiMovieRecommendationValidator(
    IMovieIdentityResolver identityResolver,
    IAiTasteProfileDataSource tasteProfileDataSource) : IAiMovieRecommendationValidator
{
    public async Task<AiValidationResult> ValidateAsync(
        Guid userId,
        IReadOnlyList<AiProviderSuggestion> suggestions,
        AiRecommendationSessionState session,
        int maxReturnedCount,
        CancellationToken cancellationToken = default)
    {
        var watchedMovieIds = await tasteProfileDataSource.GetWatchedMovieIdsAsync(userId, cancellationToken);
        var accepted = new List<AiValidatedRecommendation>();
        var seenMovieIds = new HashSet<Guid>();
        var rejectedCount = 0;

        foreach (var suggestion in suggestions)
        {
            if (!string.Equals(suggestion.MediaType, "movie", StringComparison.OrdinalIgnoreCase))
            {
                rejectedCount++;
                continue;
            }

            var resolved = await identityResolver.ResolveAsync(suggestion, cancellationToken);
            if (resolved is null)
            {
                rejectedCount++;
                continue;
            }

            if (!seenMovieIds.Add(resolved.MovieId))
            {
                rejectedCount++;
                continue;
            }

            if (session.RecommendedMovieIds.Contains(resolved.MovieId) ||
                (resolved.TmdbId.HasValue && session.RecommendedTmdbIds.Contains(resolved.TmdbId.Value)))
            {
                rejectedCount++;
                continue;
            }

            if (watchedMovieIds.Contains(resolved.MovieId))
            {
                rejectedCount++;
                continue;
            }

            if (ViolatesGenreExclusion(resolved, session.ExcludedGenres))
            {
                rejectedCount++;
                continue;
            }

            if (ViolatesRuntimeConstraint(resolved, session.MaxRuntimeMinutes))
            {
                rejectedCount++;
                continue;
            }

            if (ViolatesYearConstraint(resolved, session.MinYear, session.MaxYear))
            {
                rejectedCount++;
                continue;
            }

            accepted.Add(new AiValidatedRecommendation(resolved, suggestion.Reason));

            if (accepted.Count >= maxReturnedCount)
            {
                break;
            }
        }

        var partialResults = accepted.Count > 0 && accepted.Count < maxReturnedCount;
        return new AiValidationResult(
            accepted,
            suggestions.Count,
            accepted.Count,
            suggestions.Count - accepted.Count,
            partialResults);
    }

    private static bool ViolatesGenreExclusion(ResolvedMovieIdentity movie, List<string> excludedGenres)
    {
        if (excludedGenres.Count == 0)
        {
            return false;
        }

        var excluded = new HashSet<string>(excludedGenres, StringComparer.OrdinalIgnoreCase);
        return movie.Genres.Any(genre => excluded.Contains(genre));
    }

    private static bool ViolatesRuntimeConstraint(ResolvedMovieIdentity movie, int? maxRuntimeMinutes)
    {
        if (!maxRuntimeMinutes.HasValue || !movie.RuntimeMinutes.HasValue)
        {
            return false;
        }

        return movie.RuntimeMinutes.Value > maxRuntimeMinutes.Value;
    }

    private static bool ViolatesYearConstraint(ResolvedMovieIdentity movie, int? minYear, int? maxYear)
    {
        if (!movie.Year.HasValue)
        {
            return minYear.HasValue || maxYear.HasValue;
        }

        if (minYear.HasValue && movie.Year.Value < minYear.Value)
        {
            return true;
        }

        if (maxYear.HasValue && movie.Year.Value > maxYear.Value)
        {
            return true;
        }

        return false;
    }
}
