namespace MovieApp.Application.Exceptions;

public sealed class KeywordTranslationProviderException : Exception
{
    public KeywordTranslationProviderException(string message)
        : base(message)
    {
    }

    public KeywordTranslationProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
