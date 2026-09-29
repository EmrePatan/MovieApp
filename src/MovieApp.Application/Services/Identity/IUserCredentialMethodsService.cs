using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IUserCredentialMethodsService
{
    Task<UserProfileResult> LinkExternalLoginAsync(
        LinkExternalLoginCommand command,
        CancellationToken cancellationToken = default);

    Task<UserProfileResult> UnlinkExternalLoginAsync(
        UnlinkExternalLoginCommand command,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> CreatePasswordAsync(
        CreatePasswordCommand command,
        CancellationToken cancellationToken = default);

    Task<MessageResult> RequestEmailChangeAsync(
        RequestEmailChangeCommand command,
        CancellationToken cancellationToken = default);

    Task<MessageResult> ResendPendingEmailChangeAsync(
        string contentLocale,
        CancellationToken cancellationToken = default);
}

public sealed record LinkExternalLoginCommand(
    AccountReauthenticationProof CurrentAccountProof,
    string TargetProvider,
    string TargetIdentityToken);

public sealed record UnlinkExternalLoginCommand(
    string Provider,
    AccountReauthenticationProof CurrentAccountProof);

public sealed record CreatePasswordCommand(
    string NewPassword,
    string Provider,
    string IdentityToken);

public sealed record RequestEmailChangeCommand(
    string NewEmail,
    AccountReauthenticationProof CurrentAccountProof,
    string ContentLocale);
