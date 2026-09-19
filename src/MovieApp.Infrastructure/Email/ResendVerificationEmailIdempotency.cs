namespace MovieApp.Infrastructure.Email;

internal static class ResendVerificationEmailIdempotency
{
    internal static string CreateKey(Guid tokenId) => $"email-verification/{tokenId:D}";
}
