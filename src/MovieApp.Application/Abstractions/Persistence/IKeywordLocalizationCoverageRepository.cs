using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IKeywordLocalizationCoverageRepository
{
    Task<int> GetEligibleKeywordCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KeywordLocalizationLocaleCoverage>> GetLocaleCoverageAsync(
        CancellationToken cancellationToken = default);
}
