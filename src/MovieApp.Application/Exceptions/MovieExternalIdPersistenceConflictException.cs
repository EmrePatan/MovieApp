namespace MovieApp.Application.Exceptions;

public sealed class MovieExternalIdPersistenceConflictException : Exception
{
    public MovieExternalIdPersistenceConflictException(int? tmdbId, string externalId)
        : base("Movie external identifier persistence conflict.")
    {
        TmdbId = tmdbId;
        ExternalId = externalId;
    }

    public MovieExternalIdPersistenceConflictException(
        int? tmdbId,
        string externalId,
        Exception innerException)
        : base("Movie external identifier persistence conflict.", innerException)
    {
        TmdbId = tmdbId;
        ExternalId = externalId;
    }

    public int? TmdbId { get; }

    public string ExternalId { get; }
}
