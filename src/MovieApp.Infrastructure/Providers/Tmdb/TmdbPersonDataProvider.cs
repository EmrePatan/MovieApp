using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbPersonDataProvider(TmdbApiClient apiClient) : IPersonDataProvider
{
    public async Task<PersonProviderSearchResult> SearchPersonsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var response = await apiClient.GetCanonicalAsync<TmdbPersonSearchResponseJson>(
            $"search/person?query={encodedQuery}&include_adult=false&page={page}",
            cancellationToken);

        if (response is null)
        {
            return new PersonProviderSearchResult(
                [],
                page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Where(result => result.Id > 0 && !string.IsNullOrWhiteSpace(result.Name))
            .Select(TmdbPersonMapper.ToSummary)
            .ToList();

        return new PersonProviderSearchResult(
            results,
            response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    public async Task<PersonProviderDetails?> GetPersonAsync(
        int tmdbPersonId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var person = await apiClient.GetCanonicalAsync<TmdbPersonJson>(
                $"person/{tmdbPersonId}?append_to_response=combined_credits",
                cancellationToken);

            if (person is null || person.Id <= 0 || string.IsNullOrWhiteSpace(person.Name))
            {
                return null;
            }

            return TmdbPersonMapper.ToPersonProviderDetails(
                person,
                person.CombinedCredits ?? new TmdbCombinedCreditsResponseJson());
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
