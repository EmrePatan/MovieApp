using MovieApp.Application.Models.Images;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Localization;

public sealed class ContentLocalizedPosterSynchronizerTests
{
    [Fact]
    public async Task SyncRemovesStaleLocalizedRowWhenProviderNoLongerHasTurkishPoster()
    {
        var repository = new InMemoryContentLocalizedPosterRepository();
        var synchronizer = new ContentLocalizedPosterSynchronizer(repository);
        var contentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await repository.UpsertAsync(
            CatalogContentType.Tv,
            contentId,
            SupportedArtworkLanguageKeys.Turkish,
            "/stale-tr.jpg",
            DateTime.UtcNow);

        await synchronizer.SyncFromProviderPostersAsync(
            CatalogContentType.Tv,
            contentId,
            "/canonical-en.jpg",
            [new ProviderImageResult("/canonical-en.jpg", "en", 0.667m, 500, 750, 8m, 100)],
            originalLanguage: "en",
            CancellationToken.None);

        var paths = await repository.GetPosterPathsAsync(
            [new(CatalogContentType.Tv, contentId)],
            SupportedArtworkLanguageKeys.Turkish,
            CancellationToken.None);

        Assert.Empty(paths);
    }
}
