using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;
using MovieApp.Application.Services.Localization;
using MovieApp.UnitTests.Localization;

namespace MovieApp.UnitTests.Library;

public sealed class LibrarySearchCountModeTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task SearchLibraryAsync_UsesSkipCountModeForCategoryFetches()
    {
        var repository = new RecordingLibraryRepository();
        var service = CreateService(repository);

        await service.SearchLibraryAsync(
            new LibrarySearchCriteria("the", SearchContentType.All, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(LibrarySearchAggregator.Categories.Count, repository.Requests.Count);
        Assert.All(repository.Requests, request =>
        {
            Assert.Equal(LibraryCountMode.Skip, request.CountMode);
            Assert.Equal(1, request.Page);
            Assert.Equal(LibrarySearchAggregator.MaxItemsFetchedPerCategory, request.PageSize);
            Assert.Null(request.AfterCursor);
        });
    }

    [Fact]
    public async Task GetLibraryAsync_UsesRequiredCountModeForNormalPagination()
    {
        var repository = new RecordingLibraryRepository();
        var service = CreateService(repository);

        await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Liked, SearchContentType.All, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(LibraryCountMode.Required, repository.RequestedCountModes.Single());
    }

    [Fact]
    public async Task SearchLibraryAsync_MergesCategoryResultsByTitlePriority()
    {
        var alphaMovieId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var repository = new RecordingLibraryRepository
        {
            CategoryItems = new Dictionary<LibraryCategory, IReadOnlyList<LibraryItemResult>>
            {
                [LibraryCategory.Watching] = [CreateItem("Zulu", "tv", LibraryCategory.Watching)],
                [LibraryCategory.Watched] = [CreateItem("Alpha Movie", "movie", LibraryCategory.Watched, alphaMovieId)],
                [LibraryCategory.Liked] = [CreateItem("Alpha Movie", "movie", LibraryCategory.Liked, alphaMovieId)],
                [LibraryCategory.Watchlist] = [CreateItem("Beta", "tv", LibraryCategory.Watchlist)],
            },
        };
        var service = CreateService(repository);

        var result = await service.SearchLibraryAsync(
            new LibrarySearchCriteria("alpha", SearchContentType.All, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Alpha Movie"], result.Items.Select(item => item.Title).ToList());
        Assert.Equal(1, result.TotalCount);
    }

    private static LibraryService CreateService(RecordingLibraryRepository repository) =>
        new(
            repository,
            new AuthenticatedCurrentUser(UserId),
            new EmptyContentLocalizedPosterRepository(),
            new EmptyOriginalLanguageMovieRepository(),
            new StubTvOriginalLanguageRepository(new Dictionary<Guid, string?>()),
            NullLogger<LibraryService>.Instance);

    private static LibraryItemResult CreateItem(
        string title,
        string type,
        LibraryCategory category,
        Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            type,
            title,
            null,
            "/poster.jpg",
            null,
            2024,
            8,
            null,
            null,
            DateTime.UtcNow,
            null,
            null,
            category switch
            {
                LibraryCategory.Watching => "watching",
                LibraryCategory.Watched => "watched",
                LibraryCategory.Liked => "liked",
                _ => "watchlist",
            });

    private sealed class AuthenticatedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class RecordingLibraryRepository : ILibraryRepository
    {
        public List<LibraryCountMode> RequestedCountModes =>
            Requests.Select(request => request.CountMode).ToList();

        public List<LibraryPageRequest> Requests { get; } = [];

        public Dictionary<LibraryCategory, IReadOnlyList<LibraryItemResult>> CategoryItems { get; init; } =
            new()
            {
                [LibraryCategory.Watching] = [],
                [LibraryCategory.Watched] = [],
                [LibraryCategory.Liked] = [],
                [LibraryCategory.Watchlist] = [],
            };

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchingAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watching, request);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watched, request);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetLikedAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Liked, request);

        public Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchlistAsync(
            Guid userId,
            SearchContentType mediaType,
            LibraryPageRequest request,
            CancellationToken cancellationToken = default) =>
            RecordAndReturn(LibraryCategory.Watchlist, request);

        private Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> RecordAndReturn(
            LibraryCategory category,
            LibraryPageRequest request)
        {
            Requests.Add(request);
            var items = CategoryItems.GetValueOrDefault(category) ?? [];
            if (!request.TitleMatch.IsEmpty)
            {
                items = items
                    .Where(item => item.Title.Contains(request.TitleMatch.Primary, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return Task.FromResult(((IReadOnlyList<LibraryItemResult>)items, items.Count));
        }
    }
}
