using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.People;

namespace MovieApp.Application.Services.People;

public sealed class GetPersonByTmdbIdService(
    IPersonDataProvider personDataProvider,
    IPersonRepository personRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    ICacheService cacheService) : IGetPersonByTmdbIdService
{
    private static readonly TimeSpan DetailsCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<PersonDetailResult> GetAsync(int tmdbPersonId, CancellationToken cancellationToken = default)
    {
        if (tmdbPersonId <= 0)
        {
            throw new ValidationException("A valid TMDB person id is required.");
        }

        var cacheKey = PersonDetailsCacheKeys.Create(tmdbPersonId);
        var cachedEntry = await cacheService.GetAsync<PersonDetailsCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var existingPerson = await personRepository.GetByTmdbIdAsync(tmdbPersonId, cancellationToken);
        var providerDetails = await personDataProvider.GetPersonAsync(tmdbPersonId, cancellationToken);
        if (providerDetails is null)
        {
            throw new NotFoundException("The requested person was not found.");
        }

        var person = existingPerson is null ||
                     existingPerson.Name != providerDetails.Name ||
                     existingPerson.ProfilePath != providerDetails.ProfilePath
            ? await personRepository.UpsertFromProviderAsync(
                providerDetails.TmdbId,
                providerDetails.Name,
                providerDetails.ProfilePath,
                cancellationToken)
            : existingPerson;

        var filmography = await PersonFilmographyComposer.ComposeAsync(
            providerDetails.FilmographyCredits,
            movieRepository,
            tvShowRepository,
            cancellationToken);

        var result = new PersonDetailResult(
            person.Id,
            providerDetails.TmdbId,
            providerDetails.Name,
            providerDetails.ProfilePath,
            providerDetails.Biography,
            providerDetails.Birthday,
            providerDetails.Deathday,
            providerDetails.PlaceOfBirth,
            providerDetails.KnownForDepartment,
            filmography);

        await cacheService.SetAsync(
            cacheKey,
            new PersonDetailsCacheEntry { Result = result },
            DetailsCacheTtl,
            cancellationToken);

        return result;
    }
}
