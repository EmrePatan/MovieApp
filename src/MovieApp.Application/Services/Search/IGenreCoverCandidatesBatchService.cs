using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IGenreCoverCandidatesBatchService
{
    Task<IReadOnlyList<GenreCoverCandidatesGroupResult>> GetCandidatesAsync(
        GenreCoverCandidatesBatchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
