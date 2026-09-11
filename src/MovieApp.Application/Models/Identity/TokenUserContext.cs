namespace MovieApp.Application.Models.Identity;

public sealed record TokenUserContext(Guid UserId, string Email, Guid SecurityStamp);
