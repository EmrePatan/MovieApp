namespace MovieApp.Application.Configuration;

public sealed class KeywordDisplayProfileOptions
{
    public const string SectionName = "KeywordDisplayProfile";

    public bool Enabled { get; set; } = true;

    /// <summary>Minimum catalog document frequency for a keyword to be displayable.</summary>
    public int MinimumDocumentFrequency { get; set; } = 3;

    /// <summary>Keywords appearing on at least this share of catalog documents are treated as too generic for display.</summary>
    public double MaximumGenericDocumentFrequencyRatio { get; set; } = 0.12;

    public int MinimumNameLength { get; set; } = 3;

    public int MaximumNameLength { get; set; } = 48;

    public int MaximumTokenCount { get; set; } = 5;
}
