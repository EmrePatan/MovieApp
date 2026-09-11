namespace MovieApp.Application.Abstractions.Identity;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }
}
