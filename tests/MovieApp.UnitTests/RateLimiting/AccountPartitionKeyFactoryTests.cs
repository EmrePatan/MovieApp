using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MovieApp.Api.RateLimiting;

namespace MovieApp.UnitTests.RateLimiting;

public sealed class AccountPartitionKeyFactoryTests
{
    [Fact]
    public void CreateUsesAuthenticatedUserIdWhenAvailable()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "user-123")],
                authenticationType: "Bearer")),
            Request = { Path = "/api/users/me" }
        };

        var partitionKey = AccountPartitionKeyFactory.Create(httpContext);

        Assert.Equal("user:user-123:/api/users/me", partitionKey);
    }

    [Fact]
    public void CreateFallsBackToClientIpWhenUserIsNotAuthenticated()
    {
        var httpContext = new DefaultHttpContext
        {
            Request = { Path = "/api/users/me" },
            Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10") }
        };

        var partitionKey = AccountPartitionKeyFactory.Create(httpContext);

        Assert.Equal("ip:203.0.113.10:/api/users/me", partitionKey);
    }
}
