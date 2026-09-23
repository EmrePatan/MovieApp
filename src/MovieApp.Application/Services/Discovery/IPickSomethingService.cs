using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Services.Discovery;

public interface IPickSomethingService
{
    Task<RecommendationItem?> PickAsync(
        PickSomethingCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
