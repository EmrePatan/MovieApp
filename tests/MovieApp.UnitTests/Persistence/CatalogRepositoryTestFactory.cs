using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

internal static class CatalogRepositoryTestFactory
{
    internal static MovieRepository CreateMovieRepository(ApplicationDbContext context) =>
        new(context, new ContentSearchTitleSynchronizer(context));

    internal static TvShowRepository CreateTvShowRepository(ApplicationDbContext context) =>
        new(context, new ContentSearchTitleSynchronizer(context));
}
