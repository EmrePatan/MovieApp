namespace MovieApp.Application.Models.Keywords;

public sealed class KeywordGraphDataIntegrityException : InvalidOperationException
{
    public KeywordGraphDataIntegrityException(string message)
        : base(message)
    {
    }
}
