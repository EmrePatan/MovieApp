using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class ContentLocalizedPoster
{
    public Guid Id { get; set; }

    public CatalogContentType ContentType { get; set; }

    public Guid ContentId { get; set; }

    public string LanguageKey { get; set; } = string.Empty;

    public string PosterPath { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
}
