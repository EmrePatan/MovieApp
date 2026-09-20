using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        var existingService = CreateService(new FakeUserRepository(existingUser), new RecordingEnqueuer());
        var missingService = CreateService(new FakeUserRepository(null), new RecordingEnqueuer());

        var existingResult = await existingService.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));
        var missingResult = await missingService.ForgotPasswordAsync(new ForgotPasswordRequest("missing@example.com"));

        Assert.Equal(ForgotPasswordService.SuccessMessage, existingResult.Message);
        Assert.Equal(existingResult.Message, missingResult.Message);
    }

    [Fact]
    public async Task ForgotPasswordAsyncCreatesHashedTokenAndEnqueuesDelivery()
    {
        var user = CreateUser();
        var tokenRepository = new FakePasswordResetTokenRepository();
        var enqueuer = new RecordingEnqueuer();
        var service = CreateService(new FakeUserRepository(user), enqueuer, tokenRepository);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Single(tokenRepository.CreatedTokens);
        Assert.Single(enqueuer.EnqueuedTokenIds);
        Assert.NotNull(tokenRepository.CreatedTokens[0].ProtectedDeliverySecret);
        Assert.NotNull(tokenRepository.CreatedTokens[0].TokenHash);
    }

    [Fact]
    public async Task ForgotPasswordAsyncInvalidatesPreviousActiveTokens()
    {
        var user = CreateUser();
        var tokenRepository = new FakePasswordResetTokenRepository();
        var service = CreateService(new FakeUserRepository(user), new RecordingEnqueuer(), tokenRepository);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));
        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Equal(2, tokenRepository.CreatedTokens.Count);
        Assert.Equal(2, tokenRepository.InvalidationCount);
    }

    [Fact]
    public async Task ForgotPasswordAsyncDoesNotCreateTokenForSocialOnlyUser()
    {
        var user = CreateSocialOnlyUser();
        var tokenRepository = new FakePasswordResetTokenRepository();
        var enqueuer = new RecordingEnqueuer();
        var service = CreateService(new FakeUserRepository(user), enqueuer, tokenRepository);

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest("social@example.com"));

        Assert.Equal(ForgotPasswordService.SuccessMessage, result.Message);
        Assert.Empty(tokenRepository.CreatedTokens);
        Assert.Empty(enqueuer.EnqueuedTokenIds);
        Assert.Equal(0, tokenRepository.InvalidationCount);
    }

    [Fact]
    public async Task ForgotPasswordAsyncReturnsSameMessageForSocialOnlyAndMissingEmail()
    {
        var socialOnlyService = CreateService(
            new FakeUserRepository(CreateSocialOnlyUser()),
            new RecordingEnqueuer());
        var missingService = CreateService(new FakeUserRepository(null), new RecordingEnqueuer());

        var socialOnlyResult = await socialOnlyService.ForgotPasswordAsync(
            new ForgotPasswordRequest("social@example.com"));
        var missingResult = await missingService.ForgotPasswordAsync(
            new ForgotPasswordRequest("missing@example.com"));

        Assert.Equal(ForgotPasswordService.SuccessMessage, socialOnlyResult.Message);
        Assert.Equal(socialOnlyResult.Message, missingResult.Message);
    }

    [Fact]
    public async Task ForgotPasswordAsyncDoesNotEnqueueForInactiveUser()
    {
        var user = CreateUser();
        user.IsActive = false;
        var enqueuer = new RecordingEnqueuer();
        var service = CreateService(new FakeUserRepository(user), enqueuer);

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Equal(ForgotPasswordService.SuccessMessage, result.Message);
        Assert.Empty(enqueuer.EnqueuedTokenIds);
    }

    [Fact]
    public async Task ForgotPasswordAsyncThrowsValidationExceptionForInvalidEmail()
    {
        var service = CreateService(new FakeUserRepository(null), new RecordingEnqueuer());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ForgotPasswordAsync(new ForgotPasswordRequest("not-an-email")));
    }

    [Fact]
    public async Task ForgotPasswordAsyncReturnsGenericSuccessWhenEnqueueFails()
    {
        var user = CreateUser();
        var missingService = CreateService(new FakeUserRepository(null), new RecordingEnqueuer());
        var failingService = CreateService(new FakeUserRepository(user), new FailingEnqueuer());

        var missingResult = await missingService.ForgotPasswordAsync(new ForgotPasswordRequest("missing@example.com"));
        var failingResult = await failingService.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Equal(ForgotPasswordService.SuccessMessage, missingResult.Message);
        Assert.Equal(ForgotPasswordService.SuccessMessage, failingResult.Message);
        Assert.Equal(missingResult.Message, failingResult.Message);
    }

    [Fact]
    public async Task ForgotPasswordAsyncEnqueueFailureStillPersistsResetToken()
    {
        var user = CreateUser();
        var tokenRepository = new FakePasswordResetTokenRepository();
        var service = CreateService(new FakeUserRepository(user), new FailingEnqueuer(), tokenRepository);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Single(tokenRepository.CreatedTokens);
    }

    [Fact]
    public async Task ForgotPasswordAsyncEnqueueFailureDoesNotExposeSensitiveDetailsInResponse()
    {
        const string sensitiveMarker = "EnqueueFailureDetail";
        var user = CreateUser();
        var service = CreateService(new FakeUserRepository(user), new FailingEnqueuer(sensitiveMarker));

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Equal(ForgotPasswordService.SuccessMessage, result.Message);
        Assert.DoesNotContain(sensitiveMarker, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token=", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPasswordAsyncEnqueueFailureLogsWithoutSensitiveValues()
    {
        const string rawTokenMarker = "raw-token-marker-value";
        var user = CreateUser();
        var logger = new CollectingLogger<ForgotPasswordService>();
        var service = CreateService(
            new FakeUserRepository(user),
            new FailingEnqueuer("enqueue failed"),
            protector: new TrackingProtector(rawTokenMarker),
            logger: logger);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain(rawTokenMarker, message, StringComparison.Ordinal);
            Assert.DoesNotContain("movieapp://reset-password?token=", message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(nameof(InvalidOperationException), message, StringComparison.Ordinal);
        });
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
        IPasswordResetDeliveryEnqueuer enqueuer,
        FakePasswordResetTokenRepository? tokenRepository = null,
        IPasswordResetDeliverySecretProtector? protector = null,
        ILogger<ForgotPasswordService>? logger = null)
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
            protector ?? new PassthroughProtector(),
            enqueuer,
            options,
            logger ?? NullLogger<ForgotPasswordService>.Instance);
    }

    private static User CreateUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private static User CreateSocialOnlyUser() =>
        User.CreateFromExternalIdentity(
            Guid.NewGuid(),
            "social@example.com",
            "Social User",
            DateTime.UtcNow);

    private sealed class PassthroughProtector : IPasswordResetDeliverySecretProtector
    {
        public string Protect(string rawToken) => rawToken;

        public string Unprotect(string protectedPayload) => protectedPayload;
    }

    private sealed class TrackingProtector(string marker) : IPasswordResetDeliverySecretProtector
    {
        public string Protect(string rawToken) => marker;

        public string Unprotect(string protectedPayload) => protectedPayload;
    }

    private sealed class RecordingEnqueuer : IPasswordResetDeliveryEnqueuer
    {
        public List<Guid> EnqueuedTokenIds { get; } = [];

        public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
        {
            EnqueuedTokenIds.Add(tokenId);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEnqueuer(string failureDetail = "EnqueueFailureDetail") : IPasswordResetDeliveryEnqueuer
    {
        public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"Enqueue failed: {failureDetail}");
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
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

        public Task<PasswordResetDeliveryTarget?> GetDeliveryTargetAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PasswordResetDeliveryTarget?>(null);

        public Task CompleteDeliveryAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
