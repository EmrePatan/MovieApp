using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.Providers;

public sealed record ProviderSearchTitleEntry(
    string Title,
    ContentSearchTitleKind TitleKind,
    ContentSearchTitleSource Source,
    string? LanguageCode,
    string? CountryCode,
    string? ProviderTitleType);
