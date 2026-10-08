using System.Security.Claims;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Identity;

namespace MovieApp.Api.Identity;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated =>
        CurrentUserAmbient.UserId is not null
        || httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid? UserId => CurrentUserAmbient.UserId ?? ReadHttpUserId();

    private Guid? ReadHttpUserId()
    {
        var userIdValue = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}
