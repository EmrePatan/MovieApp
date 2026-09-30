using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordLocalizationCoverageServiceTests
{
    [Fact]
    public async Task GetCoverageAsync_ReturnsAllSixTargetLocales()
    {
        var service = new KeywordLocalizationCoverageService(new FakeKeywordLocalizationCoverageRepository());
        var rows = await service.GetCoverageAsync();

        Assert.Equal(6, rows.Count);
        Assert.Equal(SupportedContentLocales.KeywordBulkTranslationTargetLocales, rows.Select(row => row.Locale).ToList());
    }

    [Fact]
    public async Task GetCoverageAsync_ReportsAggregatesFromRepository()
    {
        var service = new KeywordLocalizationCoverageService(new FakeKeywordLocalizationCoverageRepository
        {
            Rows =
            [
                new KeywordLocalizationLocaleCoverage(
                    SupportedContentLocales.TurkishTurkey,
                    6547,
                    25,
                    0.38m,
                    6522,
                    25,
                    0,
                    0,
                    0),
            ],
        });

        var row = Assert.Single(await service.GetCoverageAsync());
        Assert.Equal(25, row.Localized);
        Assert.Equal(6522, row.Missing);
    }

    private sealed class FakeKeywordLocalizationCoverageRepository : IKeywordLocalizationCoverageRepository
    {
        public IReadOnlyList<KeywordLocalizationLocaleCoverage> Rows { get; init; } =
            SupportedContentLocales.KeywordBulkTranslationTargetLocales
                .Select(locale => new KeywordLocalizationLocaleCoverage(locale, 0, 0, 0, 0, 0, 0, 0, 0))
                .ToList();

        public Task<int> GetEligibleKeywordCountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<KeywordLocalizationLocaleCoverage>> GetLocaleCoverageAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows);
    }
}
