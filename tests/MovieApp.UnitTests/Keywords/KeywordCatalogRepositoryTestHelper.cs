using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Keywords;

internal static class KeywordCatalogRepositoryTestHelper
{
    public static KeywordCatalogRepository CreateRepository(
        ApplicationDbContext context,
        bool providerAwareSyncEnabled = false) =>
        new(
            context,
            Options.Create(new KeywordGraphOptions
            {
                ProviderAwareSyncEnabled = providerAwareSyncEnabled,
            }));
}
