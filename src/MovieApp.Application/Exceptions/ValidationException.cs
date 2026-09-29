namespace MovieApp.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public ValidationException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}
