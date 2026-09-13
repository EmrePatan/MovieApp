using MovieApp.Application.Models.Home;

namespace MovieApp.Application.Services.Home;

public interface IHomeGlobalSectionsProvider
{
    Task<HomeGlobalSections> GetOrLoadAsync(HomeCriteria criteria, CancellationToken cancellationToken = default);
}
