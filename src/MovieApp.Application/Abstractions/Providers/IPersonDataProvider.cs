using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface IPersonDataProvider
{
    Task<PersonProviderDetails?> GetPersonAsync(int tmdbPersonId, CancellationToken cancellationToken = default);

    Task<PersonProviderSearchResult> SearchPersonsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
