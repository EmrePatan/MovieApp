using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.People;

namespace MovieApp.Application.Services.People;

public sealed class GetPersonByTmdbIdService(
    IPersonDataProvider personDataProvider,
    IPersonRepository personRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository) : IGetPersonByTmdbIdService
{
    public async Task<PersonDetailResult> GetAsync(int tmdbPersonId, CancellationToken cancellationToken = default)
    {
        if (tmdbPersonId <= 0)
        {
            throw new ValidationException("A valid TMDB person id is required.");
        }

        var providerDetails = await personDataProvider.GetPersonAsync(tmdbPersonId, cancellationToken);
        if (providerDetails is null)
        {
            throw new NotFoundException("The requested person was not found.");
        }

        var person = await personRepository.UpsertFromProviderAsync(
            providerDetails.TmdbId,
            providerDetails.Name,
            providerDetails.ProfilePath,
            cancellationToken);

        var filmography = await PersonFilmographyComposer.ComposeAsync(
            providerDetails.FilmographyCredits,
            movieRepository,
            tvShowRepository,
            cancellationToken);

        return new PersonDetailResult(
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
    }
}
