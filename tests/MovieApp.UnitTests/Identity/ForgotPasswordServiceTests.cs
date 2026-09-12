using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class ForgotPasswordServiceTests
{
    [Fact]
    public async Task ForgotPasswordAsyncReturnsSameMessageForExistingAndMissingEmail()
    {
        var existingUser = CreateUser();
        var existingService = CreateService(new FakeUserRepository(existingUser), new CapturingEmailSender());
        var missingService = CreateService(new FakeUserRepository(null), new CapturingEmailSender());

        var existingResult = await existingService.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));
        var missingResult = await missingService.ForgotPasswordAsync(new ForgotPasswordRequest("missing@example.com"));

        Assert.Equal(ForgotPasswordService.SuccessMessage, existingResult.Message);
        Assert.Equal(existingResult.Message, missingResult.Message);
    }

    [Fact]
    public async Task ForgotPasswordAsyncCreatesHashedTokenAndSendsEmail()
    {
        var user = CreateUser();
        var tokenRepository = new FakePasswordResetTokenRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(new FakeUserRepository(user), emailSender, tokenRepository);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Single(tokenRepository.CreatedTokens);
        Assert.Single(emailSender.SentEmails);
        Assert.Equal(user.Email, emailSender.SentEmails[0].Email);
        Assert.DoesNotContain(
            emailSender.LastRawToken!,
            tokenRepository.CreatedTokens[0].TokenHash,
            StringComparison.Ordinal);
        Assert.Equal(
            PasswordResetTokenHasher.HashToken(emailSender.LastRawToken!),
            tokenRepository.CreatedTokens[0].TokenHash);
    }

    [Fact]
    public async Task ForgotPasswordAsyncInvalidatesPreviousActiveTokens()
    {
        var user = CreateUser();
        var tokenRepository = new FakePasswordResetTokenRepository();
        var service = CreateService(new FakeUserRepository(user), new CapturingEmailSender(), tokenRepository);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));
        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Equal(2, tokenRepository.CreatedTokens.Count);
        Assert.Equal(2, tokenRepository.InvalidationCount);
    }

    [Fact]
    public async Task ForgotPasswordAsyncDoesNotSendEmailForInactiveUser()
    {
        var user = CreateUser();
        user.IsActive = false;
        var emailSender = new CapturingEmailSender();
        var service = CreateService(new FakeUserRepository(user), emailSender);

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Equal(ForgotPasswordService.SuccessMessage, result.Message);
        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task ForgotPasswordAsyncThrowsValidationExceptionForInvalidEmail()
    {
        var service = CreateService(new FakeUserRepository(null), new CapturingEmailSender());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ForgotPasswordAsync(new ForgotPasswordRequest("not-an-email")));
    }

    [Fact]
    public void BuildResetUrlUsesConfiguredBaseUrl()
    {
        var url = ForgotPasswordService.BuildResetUrl(
            "movieapp://reset-password",
            "abc123");

        Assert.Equal("movieapp://reset-password?token=abc123", url);
    }

    private static ForgotPasswordService CreateService(
        FakeUserRepository userRepository,
        CapturingEmailSender emailSender,
        FakePasswordResetTokenRepository? tokenRepository = null)
    {
        tokenRepository ??= new FakePasswordResetTokenRepository();
        var options = Options.Create(new PasswordResetOptions
        {
            TokenLifetimeMinutes = 60,
            BaseUrl = "movieapp://reset-password"
        });

        return new ForgotPasswordService(
            userRepository,
            tokenRepository,
            emailSender,
            options);
    }

    private static User CreateUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<(string Email, string ResetUrl)> SentEmails { get; } = [];

        public string? LastRawToken { get; private set; }

        public Task SendPasswordResetEmailAsync(
            string toEmail,
            string resetUrl,
            CancellationToken cancellationToken = default)
        {
            SentEmails.Add((toEmail, resetUrl));
            LastRawToken = ExtractTokenFromResetUrl(resetUrl);
            return Task.CompletedTask;
        }

        private static string? ExtractTokenFromResetUrl(string resetUrl)
        {
            var queryIndex = resetUrl.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex < 0)
            {
                return null;
            }

            var query = resetUrl[(queryIndex + 1)..];
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

    private sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        public List<PasswordResetToken> CreatedTokens { get; } = [];

        public int InvalidationCount { get; private set; }

        public Task<PasswordResetToken?> GetActiveByTokenHashAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PasswordResetToken?>(null);

        public Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
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

        public Task<PasswordResetTokenConsumptionResult?> TryConsumeActiveTokenAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PasswordResetTokenConsumptionResult?>(null);
    }
}
