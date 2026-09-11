using System.Net;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbApiException : Exception
{
    public TmdbApiException(HttpStatusCode statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
