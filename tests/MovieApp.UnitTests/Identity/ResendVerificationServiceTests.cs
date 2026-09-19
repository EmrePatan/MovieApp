using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Identity;

public sealed class ResendVerificationServiceTests
{
    [Fact]
    public async Task ResendVerificationAsyncReturnsSameMessageForExistingAndMissingEmail()
    {
        var existingUser = CreateUnverifiedUser();
        var existingService = CreateService(new FakeUserRepository(existingUser), new CapturingEmailSender());
        var missingService = CreateService(new FakeUserRepository(null), new CapturingEmailSender());

        var existingResult = await existingService.ResendVerificationAsync(
            new ResendVerificationRequest("user@example.com"));
        var missingResult = await missingService.ResendVerificationAsync(
            new ResendVerificationRequest("missing@example.com"));

        Assert.Equal(ResendVerificationService.SuccessMessage, existingResult.Message);
        Assert.Equal(existingResult.Message, missingResult.Message);
    }

    [Fact]
    public async Task ResendVerificationAsyncCreatesHashedTokenAndSendsEmailForUnverifiedPasswordUser()
    {
        var user = CreateUnverifiedUser();
        var tokenRepository = new FakeEmailVerificationTokenRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(new FakeUserRepository(user), emailSender, tokenRepository);

        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));

        Assert.Single(tokenRepository.CreatedTokens);
        Assert.Single(emailSender.SentVerificationEmails);
        Assert.Equal(
            PasswordResetTokenHasher.HashToken(emailSender.LastRawToken!),
            tokenRepository.CreatedTokens[0].TokenHash);
    }

    [Fact]
    public async Task ResendVerificationAsyncInvalidatesPreviousActiveTokens()
    {
        var user = CreateUnverifiedUser();
        var tokenRepository = new FakeEmailVerificationTokenRepository();
        var service = CreateService(new FakeUserRepository(user), new CapturingEmailSender(), tokenRepository);

        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));
        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));

        Assert.Equal(2, tokenRepository.CreatedTokens.Count);
        Assert.Equal(2, tokenRepository.InvalidationCount);
    }

    [Fact]
    public async Task ResendVerificationAsyncDoesNotSendEmailForVerifiedUser()
    {
        var user = CreateUnverifiedUser();
        user.MarkEmailVerified(DateTime.UtcNow);
        var emailSender = new CapturingEmailSender();
        var service = CreateService(new FakeUserRepository(user), emailSender);

        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));

        Assert.Empty(emailSender.SentVerificationEmails);
    }

    private static ResendVerificationService CreateService(
        FakeUserRepository userRepository,
        CapturingEmailSender emailSender,
        FakeEmailVerificationTokenRepository? tokenRepository = null) =>
        new(
            userRepository,
            tokenRepository ?? new FakeEmailVerificationTokenRepository(),
            emailSender,
            Options.Create(new EmailVerificationOptions
            {
                TokenLifetimeMinutes = 1440,
                BaseUrl = "movieapp://verify-email"
            }),
            NullLogger<ResendVerificationService>.Instance);

    private static User CreateUnverifiedUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user?.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.NormalizedEmail == normalizedEmail);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeEmailVerificationTokenRepository : IEmailVerificationTokenRepository
    {
        public List<EmailVerificationToken> CreatedTokens { get; } = [];

        public int InvalidationCount { get; private set; }

        public Task<EmailVerificationTokenConsumptionResult?> TryConsumeActiveTokenAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EmailVerificationTokenConsumptionResult?>(null);

        public Task CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
        {
            CreatedTokens.Add(token);
            return Task.CompletedTask;
        }

        public Task InvalidateActiveTokensForUserAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            InvalidationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<(string Email, string VerifyUrl)> SentVerificationEmails { get; } = [];

        public string? LastRawToken { get; private set; }

        public Task SendPasswordResetEmailAsync(
            string toEmail,
            string resetUrl,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendEmailVerificationEmailAsync(
            string toEmail,
            string verifyUrl,
            CancellationToken cancellationToken = default)
        {
            SentVerificationEmails.Add((toEmail, verifyUrl));
            LastRawToken = ExtractToken(verifyUrl);
            return Task.CompletedTask;
        }

        private static string? ExtractToken(string verifyUrl)
        {
            var queryIndex = verifyUrl.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex < 0)
            {
                return null;
            }

            var query = verifyUrl[(queryIndex + 1)..];
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 &&
                    string.Equals(pair[0], "token", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }

            return null;
        }
    }
}
