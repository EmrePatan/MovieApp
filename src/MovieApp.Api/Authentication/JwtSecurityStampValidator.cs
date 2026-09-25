using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.Api.Authentication;

internal static class JwtSecurityStampValidator
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
        var currentSecurityStamp = memoryCache is not null && stampCache is not null
            ? await stampCache.GetOrLoadAsync(
                memoryCache,
                userId,
                token => userRepository.GetSecurityStampAsync(userId, token),
                context.HttpContext.RequestAborted)
            : await userRepository.GetSecurityStampAsync(
                userId,
                context.HttpContext.RequestAborted);

        if (currentSecurityStamp is null || currentSecurityStamp.Value != securityStamp)
        {
            context.Fail("The access token has been revoked.");
        }
    }
}
