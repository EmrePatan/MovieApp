namespace MovieApp.Application.Configuration;

public sealed class StreamingAvailabilityOptions
{
    public const string SectionName = "StreamingAvailability";

    public const string FakeProvider = "Fake";

    public bool Enabled { get; set; }

    /// <summary>"Fake" for local development without a key; anything else calls the real service.</summary>
    public string Provider { get; set; } = "StreamingAvailability";

    public string BaseUrl { get; set; } = "https://api.movieofthenight.com/v4/";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRequestsPerFetch { get; set; } = 20;

    public int MonthlyRequestLimit { get; set; } = 900;

    public string ReservedRegion { get; set; } = "TR";

    public int ReservedRegionRequests { get; set; } = 110;

    public int RefreshDays { get; set; } = 3;

    public int FailureCooldownHours { get; set; } = 6;

    public int MinimumItems { get; set; } = 5;

    /// <summary>Day of month on which the service resets the quota (1-28).</summary>
    public int QuotaResetDay { get; set; } = 1;

    public int ServiceListCacheDays { get; set; } = 30;

    public bool UsesFakeProvider => string.Equals(Provider, FakeProvider, StringComparison.OrdinalIgnoreCase);

    public bool IsOperational => Enabled && (UsesFakeProvider || !string.IsNullOrWhiteSpace(ApiKey));
}
