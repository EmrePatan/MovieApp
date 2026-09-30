namespace MovieApp.Application.Services.Keywords;

public interface IKeywordLocalizationCoverageService
{
    Task<IReadOnlyList<KeywordLocalizationLocaleCoverage>> GetCoverageAsync(
        CancellationToken cancellationToken = default);
}
