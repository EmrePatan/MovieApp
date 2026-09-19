namespace MovieApp.Application.Services.Localization;

internal static class LocalizationFieldFallback
{
    public static string Choose(string canonical, string? localized) =>
        string.IsNullOrWhiteSpace(localized) ? canonical : localized.Trim();

    public static string? ChooseNullable(string? canonical, string? localized) =>
        string.IsNullOrWhiteSpace(localized) ? canonical : localized.Trim();
}
