using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbKeywordsMapper
{
    public static IReadOnlyList<ProviderKeywordSummary> ToProviderKeywords(
        IEnumerable<TmdbKeywordItemJson> items) =>
        KeywordNormalization.Normalize(
            items.Select(item => new ProviderKeywordSummary(item.Id, item.Name)).ToList());
}
