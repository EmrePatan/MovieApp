namespace MovieApp.Infrastructure.Providers.MdbList;

public sealed class MdbListOptions
{
    public const string SectionName = "MDBList";

    public string BaseUrl { get; set; } = "https://api.mdblist.com/";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 8;

    public bool IsConfigured() => !string.IsNullOrWhiteSpace(ApiKey);
}
