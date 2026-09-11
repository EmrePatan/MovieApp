using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Abstractions.Identity;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(TokenUserContext user);
}
