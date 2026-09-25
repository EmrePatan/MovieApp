using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.Api.Authentication;

internal static partial class JwtSecurityStampValidator
{
    internal static async Task ValidateAsync(TokenValidatedContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var securityStampValue = context.Principal?.FindFirstValue(JwtClaimNames.SecurityStamp);

        if (!Guid.TryParse(userIdValue, out var userId) ||
            !Guid.TryParse(securityStampValue, out var securityStamp))
        {
            context.Fail("The access token is invalid.");
            return;
        }

        var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var memoryCache = context.HttpContext.RequestServices.GetService<IMemoryCache>();
        var stampCache = context.HttpContext.RequestServices.GetService<SecurityStampCache>();
        var stampStopwatch = Stopwatch.StartNew();
        var usedStampCache = memoryCache is not null && stampCache is not null;
        var currentSecurityStamp = usedStampCache
            ? await stampCache!.GetOrLoadAsync(
                memoryCache!,
                userId,
                token => userRepository.GetSecurityStampAsync(userId, token),
                context.HttpContext.RequestAborted)
            : await userRepository.GetSecurityStampAsync(
                userId,
                context.HttpContext.RequestAborted);
        stampStopwatch.Stop();

        if (IsWatchHistoryMutation(context.HttpContext.Request))
        {
            var logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()
                ?.CreateLogger(nameof(JwtSecurityStampValidator));
            if (logger is not null)
            {
                LogSecurityStampValidation(
                    logger,
                    context.HttpContext.Request.Path.Value ?? string.Empty,
                    stampStopwatch.ElapsedMilliseconds,
                    usedStampCache);
            }
        }

        if (currentSecurityStamp is null || currentSecurityStamp.Value != securityStamp)
        {
            context.Fail("The access token has been revoked.");
        }
    }

    private static bool IsWatchHistoryMutation(HttpRequest request) =>
        request.Method.Equals(HttpMethods.Post, StringComparison.OrdinalIgnoreCase) &&
        request.Path.StartsWithSegments("/api/watch-history", StringComparison.OrdinalIgnoreCase) &&
        (request.Path.Value?.Contains("/watch-state", StringComparison.OrdinalIgnoreCase) == true ||
         request.Path.Value?.Contains("/movies/", StringComparison.OrdinalIgnoreCase) == true);

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Information,
        Message = "WatchHistoryPerf SecurityStampValidation Path={Path} StampValidationMs={StampValidationMs} UsedStampCache={UsedStampCache}")]
    private static partial void LogSecurityStampValidation(
        ILogger logger,
        string path,
        long stampValidationMs,
        bool usedStampCache);
}
