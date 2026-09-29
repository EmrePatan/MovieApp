namespace MovieApp.Application.Exceptions;

public sealed class AuthenticationException : Exception
{
    public AuthenticationException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}
