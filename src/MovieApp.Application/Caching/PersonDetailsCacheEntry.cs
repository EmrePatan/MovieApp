using MovieApp.Application.Models.People;

namespace MovieApp.Application.Caching;

public sealed class PersonDetailsCacheEntry
{
    public required PersonDetailResult Result { get; init; }
}
