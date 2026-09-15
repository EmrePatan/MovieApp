using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.Keywords;

public static class KeywordNormalization
{
    public static IReadOnlyList<ProviderKeywordSummary> Normalize(
        IReadOnlyList<ProviderKeywordSummary> keywords)
    {
        if (keywords.Count == 0)
        {
            return [];
        }

        var normalized = new Dictionary<int, ProviderKeywordSummary>();

        foreach (var keyword in keywords)
        {
            if (keyword.TmdbKeywordId <= 0)
            {
                continue;
            }

            var name = keyword.Name.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            normalized[keyword.TmdbKeywordId] = new ProviderKeywordSummary(keyword.TmdbKeywordId, name);
        }

        return normalized.Values.ToList();
    }
}
