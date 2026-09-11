namespace MovieApp.Contracts.Users;

public sealed record ChangeEmailRequest(string Email, string CurrentPassword);
