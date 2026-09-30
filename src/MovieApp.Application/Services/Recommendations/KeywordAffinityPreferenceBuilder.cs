using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using Microsoft.Extensions.Options;

namespace MovieApp.Application.Services.Recommendations;

public interface IKeywordAffinityPreferenceBuilder
{
    IReadOnlyDictionary<Guid, decimal> Build(
        IReadOnlyList<UserBehaviorSignal> signals,
        RecommendationOptions options,
        DateTime utcNow);
}

public sealed class KeywordAffinityPreferenceBuilder(
    IKeywordCatalogStatisticsProvider statisticsProvider,
    IOptions<KeywordCatalogStatisticsOptions> statisticsOptions) : IKeywordAffinityPreferenceBuilder
{
    private readonly KeywordCatalogStatisticsOptions _statisticsOptions = statisticsOptions.Value;

    public IReadOnlyDictionary<Guid, decimal> Build(
        IReadOnlyList<UserBehaviorSignal> signals,
        RecommendationOptions options,
        DateTime utcNow) =>
        KeywordAffinityScorer.BuildKeywordPreferences(
            signals,
            options,
            utcNow,
            statisticsProvider.Current,
            _statisticsOptions);
}
