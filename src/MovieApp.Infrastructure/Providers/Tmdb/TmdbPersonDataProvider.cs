using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbPersonDataProvider(TmdbApiClient apiClient) : IPersonDataProvider
{
    public async Task<PersonProviderDetails?> GetPersonAsync(
        int tmdbPersonId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var person = await apiClient.GetAsync<TmdbPersonJson>(
                $"person/{tmdbPersonId}",
                cancellationToken);

            if (person is null || person.Id <= 0 || string.IsNullOrWhiteSpace(person.Name))
            {
                return null;
            }

            var combinedCredits = await apiClient.GetAsync<TmdbCombinedCreditsResponseJson>(
                $"person/{tmdbPersonId}/combined_credits",
                cancellationToken);

            return TmdbPersonMapper.ToPersonProviderDetails(
                person,
                combinedCredits ?? new TmdbCombinedCreditsResponseJson());
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
