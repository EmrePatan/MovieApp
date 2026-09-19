namespace MovieApp.Application.Exceptions;

public sealed class EmailNotVerifiedException : Exception
{
    public const string ErrorCode = "email_not_verified";

    public const string DefaultMessage =
        "Please verify your email address before signing in.";

    public EmailNotVerifiedException()
        : base(DefaultMessage)
    {
    }

    public EmailNotVerifiedException(string message)
        : base(message)
    {
    }
}
