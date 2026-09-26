using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Library;
using MovieApp.Domain.Enums;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;
using MovieApp.Application.Services.Localization;
using MovieApp.UnitTests.Localization;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryLocalizedPosterTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetLibraryAsyncUsesSingleBatchPosterLookupForLocalizedLocale()
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

        var posterRepository = new CountingContentLocalizedPosterRepository();
        var service = new LibraryService(
            repository,
            new AuthenticatedCurrentUser(UserId),
            posterRepository,
            NullLogger<LibraryService>.Instance);

        await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 24),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, posterRepository.GetPosterPathsCallCount);
    }

    [Fact]
    public async Task GetLibraryAsyncDoesNotQueryLocalizedPostersForEnglishLocale()
    {
        var posterRepository = new CountingContentLocalizedPosterRepository();
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
            posterRepository,
            NullLogger<LibraryService>.Instance);

        await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(0, posterRepository.GetPosterPathsCallCount);
    }

    [Fact]
    public async Task GetLibraryAsyncDoesNotInvokeImageProvider()
    {
        var imageProvider = new TrackingImageProvider();
        _ = imageProvider;

        var posterRepository = new InMemoryContentLocalizedPosterRepository();
        await posterRepository.UpsertAsync(
            CatalogContentType.Tv,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            SupportedArtworkLanguageKeys.Turkish,
            "/tr-poster.jpg",
            DateTime.UtcNow);

        var service = new LibraryService(
            new StubLibraryRepository
            {
                Items =
                [
                    new LibraryItemResult(
                        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                        "tv",
                        "Alev Alev",
                        null,
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
            posterRepository,
            NullLogger<LibraryService>.Instance);

        var result = await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 24),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("/tr-poster.jpg", result.Items[0].PosterUrl);
    }

    private sealed class CountingContentLocalizedPosterRepository : IContentLocalizedPosterRepository
    {
        public int GetPosterPathsCallCount { get; private set; }

        public Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> GetPosterPathsAsync(
            IReadOnlyList<ContentLocalizedPosterKey> keys,
            string languageKey,
            CancellationToken cancellationToken = default)
        {
            GetPosterPathsCallCount++;
            return Task.FromResult<IReadOnlyDictionary<ContentLocalizedPosterKey, string>>(
                new Dictionary<ContentLocalizedPosterKey, string>());
        }

        public Task UpsertAsync(
            CatalogContentType contentType,
            Guid contentId,
            string languageKey,
            string posterPath,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(
            CatalogContentType contentType,
            Guid contentId,
            string languageKey,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TrackingImageProvider : IImageProvider
    {
        public int Calls { get; private set; }

        public Task<Application.Models.Images.ProviderImagesResult?> GetMovieImagesAsync(
            int tmdbId,
            string? language,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<Application.Models.Images.ProviderImagesResult?>(null);
        }

        public Task<Application.Models.Images.ProviderImagesResult?> GetTvShowImagesAsync(
            int tmdbId,
            string? language,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<Application.Models.Images.ProviderImagesResult?>(null);
        }

        public Task<Application.Models.Images.ProviderImagesResult?> GetPersonImagesAsync(
            int tmdbPersonId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<Application.Models.Images.ProviderImagesResult?>(null);
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
