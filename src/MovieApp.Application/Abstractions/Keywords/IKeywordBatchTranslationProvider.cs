namespace MovieApp.Application.Abstractions.Keywords;

public interface IKeywordBatchTranslationProvider
{
    Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> sourceTexts,
        string targetContentLocale,
        CancellationToken cancellationToken = default);
}
