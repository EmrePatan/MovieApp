using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Identity;

public interface IResendVerificationService
{
    Task<MessageResult> ResendVerificationAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken = default);

    Task SendVerificationEmailAsync(User user, CancellationToken cancellationToken = default);
}
