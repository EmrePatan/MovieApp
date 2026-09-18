using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using static MovieApp.Application.Models.AiRecommendations.AiRecommendationQuotaMessages;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class AiRecommendationQuotaService : IAiRecommendationQuotaService
{
    private const string ReserveScript = """
        local userReserved = tonumber(redis.call('GET', KEYS[1]) or '0')
        local userCommitted = tonumber(redis.call('GET', KEYS[2]) or '0')
        if userCommitted + userReserved >= tonumber(ARGV[1]) then return {0, 1} end

        local globalReserved = tonumber(redis.call('GET', KEYS[3]) or '0')
        local globalCommitted = tonumber(redis.call('GET', KEYS[4]) or '0')
        if globalCommitted + globalReserved >= tonumber(ARGV[2]) then return {0, 2} end

        local rpmCount = tonumber(redis.call('GET', KEYS[5]) or '0')
        if rpmCount >= tonumber(ARGV[3]) then return {0, 3} end

        redis.call('INCR', KEYS[1])
        redis.call('EXPIRE', KEYS[1], ARGV[4])
        redis.call('INCR', KEYS[3])
        redis.call('EXPIRE', KEYS[3], ARGV[4])
        redis.call('INCR', KEYS[5])
        redis.call('EXPIRE', KEYS[5], ARGV[5])
        return {1, 0}
        """;

    private const string CommitScript = """
        local userReserved = tonumber(redis.call('GET', KEYS[1]) or '0')
        if userReserved > 0 then redis.call('DECR', KEYS[1]) end
        redis.call('INCR', KEYS[2])
        redis.call('EXPIRE', KEYS[2], ARGV[1])

        local globalReserved = tonumber(redis.call('GET', KEYS[3]) or '0')
        if globalReserved > 0 then redis.call('DECR', KEYS[3]) end
        redis.call('INCR', KEYS[4])
        redis.call('EXPIRE', KEYS[4], ARGV[1])
        return 1
        """;

    private const string ReleaseScript = """
        local userReserved = tonumber(redis.call('GET', KEYS[1]) or '0')
        if userReserved > 0 then redis.call('DECR', KEYS[1]) end

        local globalReserved = tonumber(redis.call('GET', KEYS[2]) or '0')
        if globalReserved > 0 then redis.call('DECR', KEYS[2]) end
        return 1
        """;

    private readonly IConnectionMultiplexer? _connectionMultiplexer;
    private readonly IOptions<AiRecommendationOptions> _options;
    private readonly IOptions<RedisOptions> _redisOptions;
    private readonly ILogger<AiRecommendationQuotaService> _logger;
    private readonly InMemoryAiRecommendationQuotaStore _inMemoryStore = new();

    public AiRecommendationQuotaService(
        IServiceProvider serviceProvider,
        IOptions<AiRecommendationOptions> options,
        IOptions<RedisOptions> redisOptions,
        ILogger<AiRecommendationQuotaService> logger)
    {
        _connectionMultiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
        _options = options;
        _redisOptions = redisOptions;
        _logger = logger;
    }

    private bool UseRedis =>
        _redisOptions.Value.IsConfigured() && _connectionMultiplexer is not null;

    public async Task<AiQuotaReservation> CheckAndReserveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (UseRedis)
        {
            return await ReserveRedisAsync(userId, cancellationToken);
        }

        return _inMemoryStore.Reserve(userId, _options.Value);
    }

    public async Task CommitAsync(
        Guid userId,
        AiQuotaReservation reservation,
        CancellationToken cancellationToken = default)
    {
        if (UseRedis)
        {
            await CommitRedisAsync(userId, cancellationToken);
            return;
        }

        _inMemoryStore.Commit(userId, reservation);
        await Task.CompletedTask;
    }

    public async Task ReleaseAsync(
        Guid userId,
        AiQuotaReservation reservation,
        CancellationToken cancellationToken = default)
    {
        if (UseRedis)
        {
            await ReleaseRedisAsync(userId, cancellationToken);
            return;
        }

        _inMemoryStore.Release(userId, reservation);
        await Task.CompletedTask;
    }

    public async Task<int> GetRemainingUserQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings = _options.Value;
        if (UseRedis)
        {
            var database = _connectionMultiplexer!.GetDatabase();
            var committed = (int)(await database.StringGetAsync(BuildUserCommittedKey(userId)));
            return Math.Max(0, settings.UserDailyMessageLimit - committed);
        }

        return _inMemoryStore.GetRemaining(userId, settings);
    }

    private async Task<AiQuotaReservation> ReserveRedisAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var settings = _options.Value;
        var keys = BuildKeys(userId);
        var database = _connectionMultiplexer!.GetDatabase();

        try
        {
            var result = (RedisResult[]?)await database.ScriptEvaluateAsync(
                ReserveScript,
                [
                    keys.UserReservedKey,
                    keys.UserCommittedKey,
                    keys.GlobalReservedKey,
                    keys.GlobalCommittedKey,
                    keys.RpmKey
                ],
                [
                    settings.UserDailyMessageLimit,
                    settings.GlobalDailyRequestCap,
                    settings.RpmLimit,
                    GetDailyTtlSeconds(),
                    60
                ]);

            if (result is null || result.Length < 2 || (int)result[0] != 1)
            {
                throw CreateQuotaExceededException(result);
            }
        }
        catch (AiRecommendationQuotaExceededException)
        {
            throw;
        }
        catch (Exception exception)
        {
            AiRecommendationQuotaLogMessages.LogRedisFailure(_logger, exception);
            throw new AiRecommendationInfrastructureUnavailableException(
                "AI recommendation quota storage is temporarily unavailable.");
        }

        return new AiQuotaReservation(Guid.NewGuid().ToString("N"));
    }

    private async Task CommitRedisAsync(Guid userId, CancellationToken cancellationToken)
    {
        var keys = BuildKeys(userId);
        var database = _connectionMultiplexer!.GetDatabase();

        try
        {
            await database.ScriptEvaluateAsync(
                CommitScript,
                [
                    keys.UserReservedKey,
                    keys.UserCommittedKey,
                    keys.GlobalReservedKey,
                    keys.GlobalCommittedKey
                ],
                [GetDailyTtlSeconds()]);
        }
        catch (Exception exception)
        {
            AiRecommendationQuotaLogMessages.LogRedisFailure(_logger, exception);
            throw new AiRecommendationInfrastructureUnavailableException(
                "AI recommendation quota storage is temporarily unavailable.");
        }
    }

    private async Task ReleaseRedisAsync(Guid userId, CancellationToken cancellationToken)
    {
        var keys = BuildKeys(userId);
        var database = _connectionMultiplexer!.GetDatabase();

        try
        {
            await database.ScriptEvaluateAsync(
                ReleaseScript,
                [keys.UserReservedKey, keys.GlobalReservedKey]);
        }
        catch (Exception exception)
        {
            AiRecommendationQuotaLogMessages.LogRedisFailure(_logger, exception);
            throw new AiRecommendationInfrastructureUnavailableException(
                "AI recommendation quota storage is temporarily unavailable.");
        }
    }

    private AiRecommendationQuotaExceededException CreateQuotaExceededException(RedisResult[]? result)
    {
        var reason = result is { Length: > 1 } ? (int)result[1] : 0;
        var dailyLimit = _options.Value.UserDailyMessageLimit;
        var message = reason switch
        {
            1 => DailyUserLimitReached(dailyLimit),
            2 => "Global AI recommendation capacity reached.",
            3 => "AI recommendation rate limit reached.",
            _ => "AI recommendation quota exceeded."
        };

        return new AiRecommendationQuotaExceededException(message);
    }

    private QuotaKeys BuildKeys(Guid userId)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var minute = DateTime.UtcNow.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var prefix = _redisOptions.Value.InstanceName;

        return new QuotaKeys(
            $"{prefix}ai:quota:user:{userId:D}:{date}:reserved",
            $"{prefix}ai:quota:user:{userId:D}:{date}:committed",
            $"{prefix}ai:quota:global:{date}:reserved",
            $"{prefix}ai:quota:global:{date}:committed",
            $"{prefix}ai:quota:rpm:{minute}");
    }

    private string BuildUserCommittedKey(Guid userId)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var prefix = _redisOptions.Value.InstanceName;
        return $"{prefix}ai:quota:user:{userId:D}:{date}:committed";
    }

    private static int GetDailyTtlSeconds() =>
        (int)Math.Ceiling((DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow).TotalSeconds) + 60;

    private sealed record QuotaKeys(
        string UserReservedKey,
        string UserCommittedKey,
        string GlobalReservedKey,
        string GlobalCommittedKey,
        string RpmKey);
}

internal sealed class InMemoryAiRecommendationQuotaStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, QuotaState> _states = new(StringComparer.Ordinal);

    public AiQuotaReservation Reserve(Guid userId, AiRecommendationOptions settings)
    {
        lock (_sync)
        {
            var now = DateTime.UtcNow;
            var userState = GetState(BuildUserKey(userId, now));
            var globalState = GetState(BuildGlobalKey(now));
            var rpmState = GetState(BuildRpmKey(now));

            if (userState.Committed + userState.Reserved >= settings.UserDailyMessageLimit)
            {
                throw new AiRecommendationQuotaExceededException(
                    DailyUserLimitReached(settings.UserDailyMessageLimit));
            }

            if (globalState.Committed + globalState.Reserved >= settings.GlobalDailyRequestCap)
            {
                throw new AiRecommendationQuotaExceededException("Global AI recommendation capacity reached.");
            }

            if (rpmState.Committed + rpmState.Reserved >= settings.RpmLimit)
            {
                throw new AiRecommendationQuotaExceededException("AI recommendation rate limit reached.");
            }

            userState.Reserved++;
            globalState.Reserved++;
            rpmState.Reserved++;

            var reservationId = Guid.NewGuid().ToString("N");
            return new AiQuotaReservation(reservationId);
        }
    }

    public void Commit(Guid userId, AiQuotaReservation reservation)
    {
        lock (_sync)
        {
            var now = DateTime.UtcNow;
            var userState = GetState(BuildUserKey(userId, now));
            var globalState = GetState(BuildGlobalKey(now));

            if (userState.Reserved > 0)
            {
                userState.Reserved--;
            }

            userState.Committed++;

            if (globalState.Reserved > 0)
            {
                globalState.Reserved--;
            }

            globalState.Committed++;
        }
    }

    public void Release(Guid userId, AiQuotaReservation reservation)
    {
        lock (_sync)
        {
            var now = DateTime.UtcNow;
            var userState = GetState(BuildUserKey(userId, now));
            var globalState = GetState(BuildGlobalKey(now));

            if (userState.Reserved > 0)
            {
                userState.Reserved--;
            }

            if (globalState.Reserved > 0)
            {
                globalState.Reserved--;
            }
        }
    }

    public int GetRemaining(Guid userId, AiRecommendationOptions settings)
    {
        lock (_sync)
        {
            var userState = GetState(BuildUserKey(userId, DateTime.UtcNow));
            return Math.Max(0, settings.UserDailyMessageLimit - userState.Committed);
        }
    }

    private QuotaState GetState(string key)
    {
        if (!_states.TryGetValue(key, out var state))
        {
            state = new QuotaState();
            _states[key] = state;
        }

        return state;
    }

    private static string BuildUserKey(Guid userId, DateTime utcNow) =>
        $"user:{userId:D}:{utcNow:yyyyMMdd}";

    private static string BuildGlobalKey(DateTime utcNow) => $"global:{utcNow:yyyyMMdd}";

    private static string BuildRpmKey(DateTime utcNow) => $"rpm:{utcNow:yyyyMMddHHmm}";

    private sealed class QuotaState
    {
        public int Reserved { get; set; }

        public int Committed { get; set; }
    }
}

internal static partial class AiRecommendationQuotaLogMessages
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "AI recommendation quota Redis operation failed.")]
    public static partial void LogRedisFailure(ILogger logger, Exception exception);
}
