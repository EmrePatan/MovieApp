using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

internal static class AiSessionConstraintMerger
{
    private const int MaxGenres = 8;
    private const int MaxMoodKeywords = 10;

    internal static void Merge(
        AiRecommendationSessionState session,
        AiProviderConstraintUpdates? updates)
    {
        if (updates is null)
        {
            return;
        }

        MergeGenres(session.DesiredGenres, updates.DesiredGenres);
        MergeGenres(session.ExcludedGenres, updates.ExcludedGenres);
        MergeMoodKeywords(session.MoodKeywords, updates.MoodKeywords);

        if (updates.MaxRuntimeMinutes is > 0 and <= 600)
        {
            session.MaxRuntimeMinutes = updates.MaxRuntimeMinutes;
        }

        if (updates.MinYear is >= 1888 and <= 2100)
        {
            session.MinYear = updates.MinYear;
        }

        if (updates.MaxYear is >= 1888 and <= 2100)
        {
            session.MaxYear = updates.MaxYear;
        }
    }

    private static void MergeGenres(List<string> target, IReadOnlyList<string> incoming)
    {
        foreach (var genre in incoming)
        {
            if (string.IsNullOrWhiteSpace(genre))
            {
                continue;
            }

            if (target.Any(existing => existing.Equals(genre, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (target.Count >= MaxGenres)
            {
                break;
            }

            target.Add(genre.Trim());
        }
    }

    private static void MergeMoodKeywords(List<string> target, IReadOnlyList<string> incoming)
    {
        foreach (var keyword in incoming)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (target.Any(existing => existing.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (target.Count >= MaxMoodKeywords)
            {
                break;
            }

            target.Add(keyword.Trim());
        }
    }
}
