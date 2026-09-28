namespace MovieApp.Contracts.AppConfig;

public sealed record AppConfigResponse(
    AppConfigMaintenanceResponse Maintenance,
    AppConfigVersionsResponse Versions,
    AppConfigFeaturesResponse Features);

public sealed record AppConfigMaintenanceResponse(bool Enabled);

public sealed record AppConfigVersionsResponse(
    AppConfigPlatformVersionResponse Ios,
    AppConfigPlatformVersionResponse Android);

public sealed record AppConfigPlatformVersionResponse(
    int MinimumBuild,
    int LatestBuild,
    string StoreUrl);

public sealed record AppConfigFeaturesResponse(
    bool AiRecommendations,
    bool ReviewTranslation);
