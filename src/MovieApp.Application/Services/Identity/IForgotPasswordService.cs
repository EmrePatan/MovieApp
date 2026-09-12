using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IForgotPasswordService
{
    Task<MessageResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);
}
