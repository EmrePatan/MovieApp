namespace MovieApp.Infrastructure.Configuration;

public sealed class AzureTranslatorOptions
{
    public const string SectionName = "AzureTranslator";

    public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";

    public string SubscriptionKey { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 10;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(SubscriptionKey) &&
        !string.IsNullOrWhiteSpace(Region);
}
