using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Models.Identity;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string ContentLocale = ContentLocaleResolver.EnglishUnitedStates);
