using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IResetPasswordService
{
    Task<MessageResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
