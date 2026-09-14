using MovieApp.Application.Models.People;

namespace MovieApp.Application.Services.People;

public interface IGetPersonByTmdbIdService
{
    Task<PersonDetailResult> GetAsync(int tmdbPersonId, CancellationToken cancellationToken = default);
}
