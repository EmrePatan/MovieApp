namespace MovieApp.Application.Exceptions;

public sealed class KeywordTranslationQuotaExceededException : Exception
{
    public KeywordTranslationQuotaExceededException()
        : base("Azure Translator quota was exceeded. Reduce batch size or retry later.")
    {
    }
}
