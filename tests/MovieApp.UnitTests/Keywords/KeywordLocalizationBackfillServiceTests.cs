using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordLocalizationBackfillServiceTests
{
    [Fact]
    public async Task BackfillAsync_InsertsMissingTranslation()
    {
        var keywordId = Guid.NewGuid();
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            Candidates =
            [
                new KeywordLocalizationBackfillCandidate(
                    keywordId,
                    "time travel",
                    hash,
                    KeywordLocalizationBackfillCandidateAction.Insert),
            ],
        };
        var provider = new FakeKeywordBatchTranslationProvider(text => $"TR:{text}");
        var service = new KeywordLocalizationBackfillService(repository, provider);

        var result = await service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.TurkishTurkey,
            25,
            25,
            0,
            1,
            false));

        Assert.Equal(1, result.Translated);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, provider.CallCount);
        Assert.Single(repository.Upserts);
        Assert.Equal("TR:time travel", repository.Upserts[0].Name);
    }

    [Fact]
    public async Task BackfillAsync_SecondRunWithCurrentHash_DoesNotCallProvider()
    {
        var repository = new FakeKeywordLocalizationBackfillRepository();
        var provider = new FakeKeywordBatchTranslationProvider(text => text);
        var service = new KeywordLocalizationBackfillService(repository, provider);

        var result = await service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.TurkishTurkey,
            25,
            25,
            0,
            1,
            false));

        Assert.Equal(0, result.Selected);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task DryRun_DoesNotCallProviderOrWrite()
    {
        var keywordId = Guid.NewGuid();
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            DryRunCounts = new KeywordLocalizationDryRunCounts(1, 0, 0, 0),
            Candidates =
            [
                new KeywordLocalizationBackfillCandidate(
                    keywordId,
                    "time travel",
                    hash,
                    KeywordLocalizationBackfillCandidateAction.Insert),
            ],
        };
        var provider = new FakeKeywordBatchTranslationProvider(text => text);
        var service = new KeywordLocalizationBackfillService(repository, provider);

        var result = await service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.TurkishTurkey,
            25,
            10,
            0,
            1,
            true));

        Assert.True(result.DryRun);
        Assert.Equal(0, provider.CallCount);
        Assert.Empty(repository.Upserts);
    }

    [Fact]
    public async Task BackfillAsync_RejectsBlankProviderOutput()
    {
        var keywordId = Guid.NewGuid();
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            Candidates =
            [
                new KeywordLocalizationBackfillCandidate(
                    keywordId,
                    "time travel",
                    hash,
                    KeywordLocalizationBackfillCandidateAction.Insert),
            ],
        };
        var provider = new FakeKeywordBatchTranslationProvider(_ => "   ");
        var service = new KeywordLocalizationBackfillService(repository, provider);

        var result = await service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.TurkishTurkey,
            25,
            25,
            0,
            1,
            false));

        Assert.Equal(1, result.Failed);
        Assert.Empty(repository.Upserts);
    }

    [Fact]
    public async Task BackfillAsync_RejectsProviderBatchMismatch()
    {
        var keywordId = Guid.NewGuid();
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            Candidates =
            [
                new KeywordLocalizationBackfillCandidate(
                    keywordId,
                    "time travel",
                    hash,
                    KeywordLocalizationBackfillCandidateAction.Insert),
            ],
        };
        var provider = new FakeKeywordBatchTranslationProvider(_ => string.Empty, Array.Empty<string>());
        var service = new KeywordLocalizationBackfillService(repository, provider);

        var result = await service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.TurkishTurkey,
            25,
            25,
            0,
            1,
            false));

        Assert.Equal(1, result.Failed);
        Assert.Empty(repository.Upserts);
    }

    [Fact]
    public async Task BackfillAsync_RespectsMaxItems()
    {
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            Candidates =
            [
                new(Guid.NewGuid(), "time travel", hash, KeywordLocalizationBackfillCandidateAction.Insert),
                new(Guid.NewGuid(), "time travel", hash, KeywordLocalizationBackfillCandidateAction.Insert),
            ],
        };
        var provider = new FakeKeywordBatchTranslationProvider(text => text);
        var service = new KeywordLocalizationBackfillService(repository, provider);

        var result = await service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.TurkishTurkey,
            25,
            1,
            0,
            1,
            false));

        Assert.Equal(1, result.Selected);
        Assert.Equal(1, provider.CallCount);
        Assert.Single(provider.LastBatch);
    }

    [Fact]
    public async Task BackfillAsync_RejectsEnglishLocale()
    {
        var service = new KeywordLocalizationBackfillService(
            new FakeKeywordLocalizationBackfillRepository(),
            new FakeKeywordBatchTranslationProvider(text => text));

        await Assert.ThrowsAsync<ArgumentException>(() => service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.EnglishUnitedStates,
            25,
            25,
            0,
            1,
            false)));
    }

    [Fact]
    public async Task BackfillAsync_RegeneratesStaleMachineCandidate()
    {
        var keywordId = Guid.NewGuid();
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            Candidates =
            [
                new KeywordLocalizationBackfillCandidate(
                    keywordId,
                    "time travel",
                    hash,
                    KeywordLocalizationBackfillCandidateAction.UpdateStaleMachine),
            ],
        };
        var provider = new FakeKeywordBatchTranslationProvider(text => $"TR:{text}");
        var service = new KeywordLocalizationBackfillService(repository, provider);

        var result = await service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.TurkishTurkey,
            25,
            25,
            0,
            1,
            false));

        Assert.Equal(1, result.Translated);
        Assert.Single(repository.Upserts);
    }

    [Fact]
    public async Task BackfillAsync_SkipsProtectedRowsReportedByRepository()
    {
        var keywordId = Guid.NewGuid();
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            Candidates =
            [
                new KeywordLocalizationBackfillCandidate(
                    keywordId,
                    "time travel",
                    hash,
                    KeywordLocalizationBackfillCandidateAction.Insert),
            ],
            UpsertHandler = upserts =>
                Task.FromResult(new KeywordLocalizationMachineUpsertResult(0, 0, upserts.Count)),
        };
        var provider = new FakeKeywordBatchTranslationProvider(text => text);
        var service = new KeywordLocalizationBackfillService(repository, provider);

        var result = await service.BackfillAsync(new KeywordLocalizationBackfillRequest(
            SupportedContentLocales.TurkishTurkey,
            25,
            25,
            0,
            1,
            false));

        Assert.Equal(1, result.SkippedCurated);
    }

    [Fact]
    public async Task BackfillAsync_ProviderFailureDoesNotWrite()
    {
        var keywordId = Guid.NewGuid();
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            Candidates =
            [
                new KeywordLocalizationBackfillCandidate(
                    keywordId,
                    "time travel",
                    hash,
                    KeywordLocalizationBackfillCandidateAction.Insert),
            ],
        };
        var provider = new ThrowingKeywordBatchTranslationProvider();
        var service = new KeywordLocalizationBackfillService(repository, provider);

        await Assert.ThrowsAsync<KeywordTranslationProviderException>(() => service.BackfillAsync(
            new KeywordLocalizationBackfillRequest(
                SupportedContentLocales.TurkishTurkey,
                25,
                25,
                0,
                1,
                false)));

        Assert.Empty(repository.Upserts);
    }

    [Fact]
    public async Task BackfillAsync_HonorsCancellation()
    {
        var keywordId = Guid.NewGuid();
        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var repository = new FakeKeywordLocalizationBackfillRepository
        {
            Candidates =
            [
                new KeywordLocalizationBackfillCandidate(
                    keywordId,
                    "time travel",
                    hash,
                    KeywordLocalizationBackfillCandidateAction.Insert),
            ],
        };
        var provider = new FakeKeywordBatchTranslationProvider(text => text);
        var service = new KeywordLocalizationBackfillService(repository, provider);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.BackfillAsync(
            new KeywordLocalizationBackfillRequest(
                SupportedContentLocales.TurkishTurkey,
                25,
                25,
                0,
                1,
                false),
            cts.Token));
    }

    [Fact]
    public void TryValidateTranslatedName_ProducesNormalizedName()
    {
        Assert.True(KeywordLocalizationTranslationValidator.TryValidateTranslatedName(
            "Zaman Yolculuğu",
            out var normalized,
            out _));
        Assert.Equal(
            KeywordDiscoverLocalizationSupport.NormalizeSearchName("Zaman Yolculuğu"),
            normalized);
    }

    [Fact]
    public void SupportedBulkLocales_ExcludeEnglish()
    {
        Assert.Equal(6, SupportedContentLocales.KeywordBulkTranslationTargetLocales.Count);
        Assert.DoesNotContain(
            SupportedContentLocales.EnglishUnitedStates,
            SupportedContentLocales.KeywordBulkTranslationTargetLocales);
    }

    private sealed class FakeKeywordBatchTranslationProvider : IKeywordBatchTranslationProvider
    {
        private readonly Func<string, string> _translateOne;
        private readonly Func<IReadOnlyList<string>, IReadOnlyList<string>>? _translateBatch;

        public FakeKeywordBatchTranslationProvider(Func<string, string> translateOne)
        {
            _translateOne = translateOne;
        }

        public FakeKeywordBatchTranslationProvider(
            Func<string, string> _,
            IReadOnlyList<string> batchResponse)
            : this(_ => string.Empty)
        {
            _translateBatch = _ => batchResponse;
        }

        public int CallCount { get; private set; }

        public IReadOnlyList<string> LastBatch { get; private set; } = [];

        public Task<IReadOnlyList<string>> TranslateAsync(
            IReadOnlyList<string> sourceTexts,
            string targetContentLocale,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastBatch = sourceTexts;
            if (_translateBatch is not null)
            {
                return Task.FromResult(_translateBatch(sourceTexts));
            }

            return Task.FromResult<IReadOnlyList<string>>(sourceTexts.Select(_translateOne).ToList());
        }
    }

    private sealed class ThrowingKeywordBatchTranslationProvider : IKeywordBatchTranslationProvider
    {
        public Task<IReadOnlyList<string>> TranslateAsync(
            IReadOnlyList<string> sourceTexts,
            string targetContentLocale,
            CancellationToken cancellationToken = default) =>
            throw new KeywordTranslationProviderException("failed");
    }

    private sealed class FakeKeywordLocalizationBackfillRepository : IKeywordLocalizationBackfillRepository
    {
        public IReadOnlyList<KeywordLocalizationBackfillCandidate> Candidates { get; init; } = [];

        public KeywordLocalizationDryRunCounts DryRunCounts { get; init; } =
            new(0, 0, 0, 0);

        public Func<IReadOnlyList<KeywordLocalizationMachineUpsert>, Task<KeywordLocalizationMachineUpsertResult>>?
            UpsertHandler { get; init; }

        public List<KeywordLocalizationMachineUpsert> Upserts { get; } = [];

        public Task<IReadOnlyList<KeywordLocalizationBackfillCandidate>> SelectCandidatesAsync(
            string locale,
            int take,
            Guid? startAfterKeywordId,
            CancellationToken cancellationToken = default)
        {
            var query = Candidates.AsEnumerable();
            if (startAfterKeywordId is not null)
            {
                query = query.Where(candidate => candidate.KeywordId.CompareTo(startAfterKeywordId.Value) > 0);
            }

            return Task.FromResult<IReadOnlyList<KeywordLocalizationBackfillCandidate>>(query.Take(take).ToList());
        }

        public Task<KeywordLocalizationMachineUpsertResult> UpsertMachineTranslationsAsync(
            IReadOnlyList<KeywordLocalizationMachineUpsert> upserts,
            CancellationToken cancellationToken = default)
        {
            if (UpsertHandler is not null)
            {
                return UpsertHandler(upserts);
            }

            Upserts.AddRange(upserts);
            return Task.FromResult(new KeywordLocalizationMachineUpsertResult(upserts.Count, 0, 0));
        }

        public Task UpsertCuratedTranslationAsync(
            KeywordLocalizationCuratedUpsert upsert,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<KeywordLocalizationDryRunCounts> GetDryRunCountsAsync(
            string locale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DryRunCounts);

        public Task<int> CountStaleProtectedAsync(string locale, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
