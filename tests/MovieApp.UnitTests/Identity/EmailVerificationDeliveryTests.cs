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

public sealed class EmailVerificationDeliveryTests
{
    [Fact]
    public async Task SendVerificationEmailAsyncEnqueuesDeliveryWithoutSendingEmailInline()
    {
        var emailSender = new RecordingEmailSender();
        var enqueuer = new RecordingEnqueuer();
        var service = CreateResendService(emailSender, enqueuer);

        await service.SendVerificationEmailAsync(CreateUnverifiedUser());

        Assert.Single(enqueuer.EnqueuedTokenIds);
        Assert.Empty(emailSender.SentVerificationEmails);
    }

    [Fact]
    public async Task SendVerificationEmailAsyncCreatesOneTokenPerUserAction()
    {
        var tokenRepository = new TrackingEmailVerificationTokenRepository();
        var service = CreateResendService(
            new RecordingEmailSender(),
            new RecordingEnqueuer(),
            tokenRepository);

        await service.SendVerificationEmailAsync(CreateUnverifiedUser());

        Assert.Single(tokenRepository.CreatedTokens);
        Assert.NotNull(tokenRepository.CreatedTokens[0].ProtectedDeliverySecret);
    }

    [Fact]
    public async Task DeliverAsyncRetryDoesNotCreateOrInvalidateTokens()
    {
        var tokenRepository = new TrackingEmailVerificationTokenRepository();
        var emailSender = new RecordingEmailSender { FailCount = 1 };
        var service = CreateDeliveryService(tokenRepository, emailSender);
        var tokenId = await SeedDeliverableTokenAsync(tokenRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeliverAsync(tokenId));
        await service.DeliverAsync(tokenId);

        Assert.Single(tokenRepository.CreatedTokens);
        Assert.Equal(0, tokenRepository.InvalidationCount);
        Assert.Single(emailSender.SentVerificationEmails);
        Assert.Contains(tokenId, tokenRepository.CompletedTokenIds);
    }

    [Fact]
    public async Task ResendInvalidatesPreviousTokenBeforeCreatingNewOne()
    {
        var tokenRepository = new TrackingEmailVerificationTokenRepository();
        var service = CreateResendService(
            new RecordingEmailSender(),
            new RecordingEnqueuer(),
            tokenRepository);
        var user = CreateUnverifiedUser();

        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));
        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));

        Assert.Equal(2, tokenRepository.CreatedTokens.Count);
        Assert.Equal(2, tokenRepository.InvalidationCount);
    }

    [Fact]
    public async Task SendVerificationEmailAsyncSurvivesEnqueueFailure()
    {
        var service = CreateResendService(
            new RecordingEmailSender(),
            new FailingEnqueuer());

        var result = await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));

        Assert.Equal(ResendVerificationService.SuccessMessage, result.Message);
    }

    [Fact]
    public async Task VerificationSucceedsAfterDelayedDelivery()
    {
        var tokenRepository = new TrackingEmailVerificationTokenRepository();
        var emailSender = new RecordingEmailSender();
        var enqueuer = new RecordingEnqueuer();
        var resendService = CreateResendService(emailSender, enqueuer, tokenRepository);
        var user = CreateUnverifiedUser();

        await resendService.SendVerificationEmailAsync(user);

        var tokenId = enqueuer.EnqueuedTokenIds.Single();
        var deliveryService = CreateDeliveryService(tokenRepository, emailSender);
        await deliveryService.DeliverAsync(tokenId);

        var rawToken = emailSender.LastRawToken!;
        var verifyService = new VerifyEmailService(
            new FakeApplicationDbContext(),
            new FakeUserRepository(user),
            tokenRepository,
            new FakeTokenService());

        var authResult = await verifyService.VerifyEmailAsync(new VerifyEmailRequest(rawToken!));

        Assert.False(string.IsNullOrWhiteSpace(authResult.AccessToken));
        Assert.True(user.IsEmailVerified);
    }

    private static ResendVerificationService CreateResendService(
        RecordingEmailSender emailSender,
        IEmailVerificationDeliveryEnqueuer enqueuer,
        TrackingEmailVerificationTokenRepository? tokenRepository = null) =>
        new(
            new FakeUserRepository(CreateUnverifiedUser()),
            tokenRepository ?? new TrackingEmailVerificationTokenRepository(),
            new PassthroughProtector(),
            enqueuer,
            Options.Create(new EmailVerificationOptions
            {
                TokenLifetimeMinutes = 1440,
                BaseUrl = "movieapp://verify-email"
            }),
            NullLogger<ResendVerificationService>.Instance);

    private static EmailVerificationDeliveryService CreateDeliveryService(
        TrackingEmailVerificationTokenRepository tokenRepository,
        RecordingEmailSender emailSender) =>
        new(
            tokenRepository,
            new PassthroughProtector(),
            emailSender,
            Options.Create(new EmailVerificationOptions
            {
                TokenLifetimeMinutes = 1440,
                BaseUrl = "movieapp://verify-email"
            }),
            NullLogger<EmailVerificationDeliveryService>.Instance);

    private static async Task<Guid> SeedDeliverableTokenAsync(
        TrackingEmailVerificationTokenRepository tokenRepository)
    {
        var rawToken = PasswordResetTokenGenerator.GenerateToken();
        var token = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = PasswordResetTokenHasher.HashToken(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            ProtectedDeliverySecret = rawToken
        };

        await tokenRepository.CreateAsync(token);
        tokenRepository.SeedDeliveryTarget(new EmailVerificationDeliveryTarget(
            token.Id,
            token.UserId,
            "user@example.com",
            rawToken,
            true));

        return token.Id;
    }

    private static User CreateUnverifiedUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private sealed class PassthroughProtector : IEmailVerificationDeliverySecretProtector
    {
        public string Protect(string rawToken) => rawToken;

        public string Unprotect(string protectedPayload) => protectedPayload;
    }

    private sealed class RecordingEnqueuer : IEmailVerificationDeliveryEnqueuer
    {
        public List<Guid> EnqueuedTokenIds { get; } = [];

        public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
        {
            EnqueuedTokenIds.Add(tokenId);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEnqueuer : IEmailVerificationDeliveryEnqueuer
    {
        public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("enqueue failed");
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public int FailCount { get; init; }

        public List<(string Email, string VerifyUrl)> SentVerificationEmails { get; } = [];

        public string? LastRawToken { get; private set; }

        private int _attempts;

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
            _attempts++;
            if (_attempts <= FailCount)
            {
                throw new InvalidOperationException("smtp failed");
            }

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

    private sealed class TrackingEmailVerificationTokenRepository : IEmailVerificationTokenRepository
    {
        private readonly Dictionary<Guid, EmailVerificationDeliveryTarget> _deliveryTargets = new();
        private readonly Dictionary<string, EmailVerificationToken> _tokensByHash = new(StringComparer.Ordinal);

        public List<EmailVerificationToken> CreatedTokens { get; } = [];

        public List<Guid> CompletedTokenIds { get; } = [];

        public int InvalidationCount { get; private set; }

        public void SeedDeliveryTarget(EmailVerificationDeliveryTarget target) =>
            _deliveryTargets[target.TokenId] = target;

        public Task<EmailVerificationTokenConsumptionResult?> TryConsumeActiveTokenAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (!_tokensByHash.TryGetValue(tokenHash, out var token) ||
                token.UsedAtUtc is not null ||
                token.ExpiresAtUtc <= utcNow)
            {
                return Task.FromResult<EmailVerificationTokenConsumptionResult?>(null);
            }

            token.UsedAtUtc = utcNow;
            return Task.FromResult<EmailVerificationTokenConsumptionResult?>(
                new EmailVerificationTokenConsumptionResult(token.Id, token.UserId));
        }

        public Task CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
        {
            CreatedTokens.Add(token);
            _tokensByHash[token.TokenHash] = token;
            _deliveryTargets[token.Id] = new EmailVerificationDeliveryTarget(
                token.Id,
                token.UserId,
                "user@example.com",
                token.ProtectedDeliverySecret ?? string.Empty,
                true);
            return Task.CompletedTask;
        }

        public Task InvalidateActiveTokensForUserAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            InvalidationCount++;
            foreach (var token in CreatedTokens.Where(token => token.UserId == userId && token.UsedAtUtc is null))
            {
                token.UsedAtUtc = utcNow;
                token.ProtectedDeliverySecret = null;
            }

            return Task.CompletedTask;
        }

        public Task<EmailVerificationDeliveryTarget?> GetDeliveryTargetAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (!_deliveryTargets.TryGetValue(tokenId, out var target))
            {
                return Task.FromResult<EmailVerificationDeliveryTarget?>(null);
            }

            var token = CreatedTokens.FirstOrDefault(item => item.Id == tokenId);
            if (token is null ||
                token.UsedAtUtc is not null ||
                token.ExpiresAtUtc <= utcNow ||
                token.ProtectedDeliverySecret is null ||
                CompletedTokenIds.Contains(tokenId))
            {
                return Task.FromResult<EmailVerificationDeliveryTarget?>(
                    target with { IsDeliverable = false });
            }

            return Task.FromResult<EmailVerificationDeliveryTarget?>(target);
        }

        public Task CompleteDeliveryAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            CompletedTokenIds.Add(tokenId);
            var token = CreatedTokens.FirstOrDefault(item => item.Id == tokenId);
            if (token is not null)
            {
                token.DeliveryCompletedAtUtc = utcNow;
                token.ProtectedDeliverySecret = null;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            action(cancellationToken);
    }

    private sealed class FakeTokenService : ITokenService
    {
        public AccessTokenResult CreateAccessToken(TokenUserContext user) =>
            new("access-token", DateTime.UtcNow.AddHours(1));
    }
}
