using System.Net;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Localization;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbLocalizedDetailDataProvider(TmdbApiClient apiClient) : ILocalizedDetailDataProvider
{
    public async Task<MovieDetailLocalizationData?> GetMovieLocalizationAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetLocalizedAsync<TmdbMovieDetailsResponseJson>(
                $"movie/{tmdbId}?append_to_response=external_ids",
                contentLocale,
                cancellationToken);

            if (response is null)
            {
                return null;
            }

            return new MovieDetailLocalizationData(
                response.Title,
                response.Overview,
                response.BelongsToCollection?.Name);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TvShowDetailLocalizationData?> GetTvShowLocalizationAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetLocalizedAsync<TmdbTvDetailsResponseJson>(
                $"tv/{tmdbId}?append_to_response=external_ids",
                contentLocale,
                cancellationToken);

            if (response is null)
            {
                return null;
            }

            var seasonNames = response.Seasons
                .Where(season => season.SeasonNumber > 0)
                .ToDictionary(
                    season => season.SeasonNumber,
                    season => season.Name);

            return new TvShowDetailLocalizationData(
                response.Name,
                response.Overview,
                response.Status,
                seasonNames);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PersonDetailLocalizationData?> GetPersonLocalizationAsync(
        int tmdbPersonId,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var person = await apiClient.GetLocalizedAsync<TmdbPersonJson>(
                $"person/{tmdbPersonId}",
                contentLocale,
                cancellationToken);

            if (person is null || person.Id <= 0)
            {
                return null;
            }

            var combinedCredits = await apiClient.GetLocalizedAsync<TmdbCombinedCreditsResponseJson>(
                $"person/{tmdbPersonId}/combined_credits",
                contentLocale,
                cancellationToken);

            var providerDetails = TmdbPersonMapper.ToPersonProviderDetails(
                person,
                combinedCredits ?? new TmdbCombinedCreditsResponseJson());

            var filmography = providerDetails.FilmographyCredits
                .ToDictionary(
                    credit => new PersonFilmographyLocalizationKey(credit.MediaType, credit.TmdbId),
                    credit => new PersonFilmographyLocalizationEntry(credit.Title, credit.Character));

            return new PersonDetailLocalizationData(person.Biography, filmography);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<CollectionDetailLocalizationData?> GetCollectionLocalizationAsync(
        int tmdbCollectionId,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetLocalizedAsync<TmdbCollectionResponseJson>(
                $"collection/{tmdbCollectionId}",
                contentLocale,
                cancellationToken);

            if (response is null || response.Id <= 0)
            {
                return null;
            }

            var parts = response.Parts
                .Where(part => part.Id > 0)
                .ToDictionary(
                    part => part.Id,
                    part => new CollectionPartLocalizationEntry(part.Title, part.Overview));

            return new CollectionDetailLocalizationData(response.Name, response.Overview, parts);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
