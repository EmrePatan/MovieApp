using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Library;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryLocalizedPosterTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ShowId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetLibraryAsyncUsesSingleBatchMetadataLookupForLocalizedLocale()
    {
        var repository = new StubLibraryRepository
        {
            Items = Enumerable.Range(0, 24)
                .Select(index => new LibraryItemResult(
                    Guid.Parse($"cccccccc-cccc-cccc-cccc-{index:D12}"),
                    "tv",
                    $"Title {index}",
                    null,
                    "/canonical.jpg",
                    null,
                    2024,
                    8m,
                    null,
                    null,
                    DateTime.UtcNow,
                    null,
                    null,
                    "watching"))
                .ToList(),
            TotalCount = 24
        };

        var metadataRepository = new CountingSummaryLocalizationMetadataReadRepository();
        var service = new LibraryService(
            repository,
            new AuthenticatedCurrentUser(UserId),
            metadataRepository,
            NullLogger<LibraryService>.Instance);

        await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 24),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, metadataRepository.LoadCallCount);
    }

    [Fact]
    public async Task GetLibraryAsyncDoesNotQueryLocalizedMetadataForEnglishLocale()
    {
        var metadataRepository = new CountingSummaryLocalizationMetadataReadRepository();
        var service = new LibraryService(
            new StubLibraryRepository
            {
                Items =
                [
                    new LibraryItemResult(
                        Guid.NewGuid(),
                        "tv",
                        "Title",
                        null,
                        "/canonical.jpg",
                        null,
                        2024,
                        8m,
                        null,
                        null,
                        DateTime.UtcNow,
                        null,
                        null,
                        "watching")
                ],
                TotalCount = 1
            },
            new AuthenticatedCurrentUser(UserId),
            metadataRepository,
            NullLogger<LibraryService>.Instance);

        await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(0, metadataRepository.LoadCallCount);
    }

    [Fact]
    public async Task GetLibraryAsyncAppliesLocalizedDisplayTitlesForTurkishLocale()
    {
        var metadataRepository = new CountingSummaryLocalizationMetadataReadRepository(
            tvContexts: new Dictionary<Guid, ContentProductionContext>
            {
                [ShowId] = new ContentProductionContext("tr", "TR"),
            },
            localizedPosters: new Dictionary<ContentLocalizedPosterKey, string>
            {
                [new ContentLocalizedPosterKey(CatalogContentType.Tv, ShowId)] = "/tr-poster.jpg",
            });

        var service = new LibraryService(
            new StubLibraryRepository
            {
                Items =
                [
                    new LibraryItemResult(
                        ShowId,
                        "tv",
                        "Ethos",
                        "Bir Başkadır",
                        "/canonical-en.jpg",
                        null,
                        2020,
                        8m,
                        null,
                        null,
                        DateTime.UtcNow,
                        null,
                        null,
                        "watching")
                ],
                TotalCount = 1
            },
            new AuthenticatedCurrentUser(UserId),
            metadataRepository,
            NullLogger<LibraryService>.Instance);

        var result = await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 24),
            ContentLocaleResolver.TurkishTurkey);

        var item = Assert.Single(result.Items);
        Assert.Equal("Bir Başkadır", item.Title);
        Assert.Equal("Ethos", item.OriginalTitle);
        Assert.Equal("/tr-poster.jpg", item.PosterUrl);
    }

    private sealed class CountingSummaryLocalizationMetadataReadRepository(
        IReadOnlyDictionary<Guid, ContentProductionContext>? tvContexts = null,
        IReadOnlyDictionary<ContentLocalizedPosterKey, string>? localizedPosters = null)
        : ISummaryLocalizationMetadataReadRepository
    {
        public int LoadCallCount { get; private set; }

        public Task<SummaryLocalizationMetadataBatch> LoadAsync(
            IReadOnlyList<Guid> movieIds,
            IReadOnlyList<Guid> tvIds,
            IReadOnlyList<ContentLocalizedPosterKey> posterKeys,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            LoadCallCount++;
            var tvs = new Dictionary<Guid, ContentProductionContext>();
            if (tvContexts is not null)
            {
                foreach (var id in tvIds)
                {
                    if (tvContexts.TryGetValue(id, out var context))
                    {
                        tvs[id] = context;
                    }
                }
            }

            return Task.FromResult(
                new SummaryLocalizationMetadataBatch(
                    new Dictionary<Guid, ContentProductionContext>(),
                    tvs,
                    new Dictionary<CatalogContentKey, string>(),
                    new Dictionary<CatalogContentKey, string>(),
                    localizedPosters ?? new Dictionary<ContentLocalizedPosterKey, string>()));
        }
    }

    private sealed class StubLibraryRepository : ILibraryRepository
    {
        public IReadOnlyList<LibraryItemResult> Items { get; init; } = [];

        public int TotalCount { get; init; }

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchingAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((Items, TotalCount));

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((Items, TotalCount));

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetLikedAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((Items, TotalCount));

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchlistAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((Items, TotalCount));
    }

    private sealed class AuthenticatedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }
}
