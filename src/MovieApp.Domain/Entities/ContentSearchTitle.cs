using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class ContentSearchTitle
{
    public Guid Id { get; set; }

    public CatalogContentType ContentType { get; set; }

    public Guid ContentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string NormalizedTitle { get; set; } = string.Empty;

    public ContentSearchTitleKind TitleKind { get; set; }

    public string? LanguageCode { get; set; }

    public string? CountryCode { get; set; }

    public ContentSearchTitleSource Source { get; set; }

    public string? ProviderTitleType { get; set; }

    public DateTime? ProviderUpdatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
