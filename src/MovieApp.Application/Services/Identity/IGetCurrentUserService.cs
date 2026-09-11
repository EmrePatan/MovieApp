using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IGetCurrentUserService
{
    Task<CurrentUserResult> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
