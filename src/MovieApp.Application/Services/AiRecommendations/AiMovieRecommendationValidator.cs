using System.Diagnostics;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class AiMovieRecommendationValidator(
    IMovieIdentityResolver identityResolver,
    IAiTasteProfileDataSource tasteProfileDataSource,
    IAiRecommendationPerfContext perfContext) : IAiMovieRecommendationValidator
{
    public async Task<AiValidationResult> ValidateAsync(
        Guid userId,
        IReadOnlyList<AiProviderSuggestion> suggestions,
        AiRecommendationSessionState session,
        int maxReturnedCount,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var watchedIdsStopwatch = Stopwatch.StartNew();
        var watchedMovieIds = await tasteProfileDataSource.GetWatchedMovieIdsAsync(userId, cancellationToken);
        var watchedTvShowIds = await tasteProfileDataSource.GetWatchedTvShowIdsAsync(userId, cancellationToken);
        watchedIdsStopwatch.Stop();

        var accepted = new List<AiValidatedRecommendation>();
        var seenContentIds = new HashSet<Guid>();
        var rejectedCount = 0;

        var resolutionStopwatch = Stopwatch.StartNew();
        foreach (var suggestion in suggestions)
        {
            if (!IsSupportedMediaType(suggestion.MediaType))
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

            if (!seenContentIds.Add(resolved.MovieId))
            {
                rejectedCount++;
                continue;
            }

            if (IsPreviouslyRecommended(resolved, session) || IsWatched(resolved, watchedMovieIds, watchedTvShowIds))
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

        resolutionStopwatch.Stop();
        totalStopwatch.Stop();
        perfContext.RecordValidationTimings(
            totalStopwatch.ElapsedMilliseconds,
            watchedIdsStopwatch.ElapsedMilliseconds,
            resolutionStopwatch.ElapsedMilliseconds);

        var partialResults = accepted.Count > 0 && accepted.Count < maxReturnedCount;
        return new AiValidationResult(
            accepted,
            suggestions.Count,
            accepted.Count,
            suggestions.Count - accepted.Count,
            partialResults);
    }

    private static bool IsSupportedMediaType(string mediaType) =>
        string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase);

    private static bool IsPreviouslyRecommended(ResolvedMovieIdentity resolved, AiRecommendationSessionState session)
    {
        if (string.Equals(resolved.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            return session.RecommendedTvShowIds.Contains(resolved.MovieId) ||
                   (resolved.TmdbId.HasValue && session.RecommendedTvTmdbIds.Contains(resolved.TmdbId.Value));
        }

        return session.RecommendedMovieIds.Contains(resolved.MovieId) ||
               (resolved.TmdbId.HasValue && session.RecommendedTmdbIds.Contains(resolved.TmdbId.Value));
    }

    private static bool IsWatched(
        ResolvedMovieIdentity resolved,
        IReadOnlySet<Guid> watchedMovieIds,
        IReadOnlySet<Guid> watchedTvShowIds)
    {
        if (string.Equals(resolved.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            return watchedTvShowIds.Contains(resolved.MovieId);
        }

        return watchedMovieIds.Contains(resolved.MovieId);
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
        if (!string.Equals(movie.MediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

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
