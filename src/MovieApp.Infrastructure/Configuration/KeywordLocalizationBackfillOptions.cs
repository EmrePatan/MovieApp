namespace MovieApp.Infrastructure.Configuration;

public sealed class KeywordLocalizationBackfillOptions
{
    public const string SectionName = "KeywordLocalizationBackfill";

    public int DefaultBatchSize { get; set; } = 25;

    public int DefaultMaxItems { get; set; } = 25;

    public int MaxBatchSize { get; set; } = 100;
}
