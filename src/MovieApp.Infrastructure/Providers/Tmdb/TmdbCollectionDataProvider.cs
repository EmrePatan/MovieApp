using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbCollectionDataProvider(TmdbApiClient apiClient) : ICollectionDataProvider
{
    public async Task<CollectionProviderDetails?> GetCollectionAsync(
        int tmdbCollectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetCanonicalAsync<TmdbCollectionResponseJson>(
                $"collection/{tmdbCollectionId}",
                cancellationToken);

            if (response is null || response.Id <= 0)
            {
                return null;
            }

            return TmdbCollectionMapper.ToCollectionProviderDetails(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
