using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class SearchProviderRefresh
{
    public Guid Id { get; set; }

    public string NormalizedQuery { get; set; } = string.Empty;

    public SearchProviderContentType ContentType { get; set; }

    public int Page { get; set; }

    public DateTime LastRefreshedAtUtc { get; set; }
}
