namespace MovieApp.Application.Models.Identity;

public sealed record RegisterUserRequest(string Email, string Password, string DisplayName);
