namespace MovieApp.Application.Abstractions.Keywords;

public interface IKeywordCatalogStatisticsProvider
{
    IKeywordCatalogStatisticsSnapshot Current { get; }

    bool IsFrequencyAwareActive { get; }
}
