using MovieApp.Application.Services.Localization;

namespace MovieApp.Api.Localization;

internal static class ContentLocaleHttpExtensions
{
    public static string ResolveContentLocale(this HttpRequest request) =>
        ContentLocaleResolver.ResolveFromAcceptLanguage(request.Headers.AcceptLanguage.ToString());
}
