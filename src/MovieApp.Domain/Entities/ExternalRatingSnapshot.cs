using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class ExternalRatingSnapshot
{
    public Guid Id { get; set; }

    public CatalogContentType MediaType { get; set; }

    public int TmdbId { get; set; }

    public ExternalRatingsProvider Provider { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime FetchedAtUtc { get; set; }
}
