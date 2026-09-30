using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Recommendations;

namespace MovieApp.Infrastructure.Keywords;

public sealed class KeywordCatalogStatisticsProvider : IKeywordCatalogStatisticsProvider
{
    private KeywordCatalogStatisticsSnapshot _current = KeywordCatalogStatisticsSnapshot.Empty;

    public IKeywordCatalogStatisticsSnapshot Current => Volatile.Read(ref _current);

    public bool IsFrequencyAwareActive => Current.IsAvailable;

    internal void Publish(KeywordCatalogStatisticsSnapshot snapshot) =>
        Volatile.Write(ref _current, snapshot);
}
