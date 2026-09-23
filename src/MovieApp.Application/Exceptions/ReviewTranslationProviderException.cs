namespace MovieApp.Application.Exceptions;

public class ReviewTranslationProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ReviewTranslationQuotaExceededException()
    : ReviewTranslationProviderException("Review translation quota exceeded.");
