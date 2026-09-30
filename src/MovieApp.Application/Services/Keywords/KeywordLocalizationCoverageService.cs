using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.Application.Services.Keywords;

public sealed class KeywordLocalizationCoverageService(
    IKeywordLocalizationCoverageRepository coverageRepository) : IKeywordLocalizationCoverageService
{
    public Task<IReadOnlyList<KeywordLocalizationLocaleCoverage>> GetCoverageAsync(
        CancellationToken cancellationToken = default) =>
        coverageRepository.GetLocaleCoverageAsync(cancellationToken);
}
