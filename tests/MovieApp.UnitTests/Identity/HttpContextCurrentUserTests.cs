using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MovieApp.Api.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class HttpContextCurrentUserTests
{
    [Fact]
    public void UserIdReturnsAuthenticatedUserIdFromSubClaim()
    {
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, "user@example.com")
            ],
            authenticationType: "Bearer"))
        };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUser = new HttpContextCurrentUser(accessor);

        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal(userId, currentUser.UserId);
    }

    [Fact]
    public void UserIdIsNullWhenUnauthenticated()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var currentUser = new HttpContextCurrentUser(accessor);

        Assert.False(currentUser.IsAuthenticated);
        Assert.Null(currentUser.UserId);
    }
}
