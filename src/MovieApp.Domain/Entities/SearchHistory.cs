namespace MovieApp.Domain.Entities;

public sealed class SearchHistory
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Query { get; set; } = string.Empty;

    public string NormalizedQuery { get; set; } = string.Empty;

    public DateTime SearchedAt { get; set; }

    public User User { get; set; } = null!;

    public static SearchHistory Create(Guid userId, string query, string normalizedQuery, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query is required.", nameof(query));
        }

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            throw new ArgumentException("Normalized query is required.", nameof(normalizedQuery));
        }

        return new SearchHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Query = query.Trim(),
            NormalizedQuery = normalizedQuery,
            SearchedAt = utcNow
        };
    }

    public void UpdateSearchedAt(DateTime utcNow)
    {
        SearchedAt = utcNow;
    }
}
