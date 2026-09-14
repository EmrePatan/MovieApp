using MovieApp.Application.Models.Credits;

namespace MovieApp.Application.Caching;

public sealed class CreditsCacheEntry
{
    public CreditsResult Result { get; init; } = new([]);
}
