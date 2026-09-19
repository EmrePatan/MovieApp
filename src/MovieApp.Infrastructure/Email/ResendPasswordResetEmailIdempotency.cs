namespace MovieApp.Infrastructure.Email;

internal static class ResendPasswordResetEmailIdempotency
{
    internal static string CreateKey(Guid tokenId) => $"password-reset/{tokenId:D}";
}
