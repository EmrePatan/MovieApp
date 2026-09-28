namespace MovieApp.Application.Configuration;

public sealed class MobileAppConfigOptions
{
    public const string SectionName = "MobileAppConfig";

    public MobileAppConfigMaintenanceOptions Maintenance { get; set; } = new();

    public MobileAppConfigVersionsOptions Versions { get; set; } = new();

    public MobileAppConfigFeaturesOptions Features { get; set; } = new();
}

public sealed class MobileAppConfigMaintenanceOptions
{
    public bool Enabled { get; set; }
}

public sealed class MobileAppConfigVersionsOptions
{
    public MobileAppConfigPlatformVersionOptions Ios { get; set; } = new();

    public MobileAppConfigPlatformVersionOptions Android { get; set; } = new();
}

public sealed class MobileAppConfigPlatformVersionOptions
{
    public int MinimumBuild { get; set; } = 9;

    public int LatestBuild { get; set; } = 9;

    public string StoreUrl { get; set; } =
        "https://apps.apple.com/app/id6814454427";
}

public sealed class MobileAppConfigFeaturesOptions
{
    public bool AiRecommendations { get; set; } = true;

    public bool ReviewTranslation { get; set; } = true;
}
