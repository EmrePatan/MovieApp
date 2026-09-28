using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MovieApp.Api.RateLimiting;
using MovieApp.Application.Configuration;
using MovieApp.Contracts.AppConfig;

namespace MovieApp.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/public/app-config")]
public sealed class PublicAppConfigController(IOptionsSnapshot<MobileAppConfigOptions> options) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting(MobileAppConfigRateLimitPolicies.Get)]
    [ProducesResponseType(typeof(AppConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult<AppConfigResponse> Get()
    {
        var config = options.Value;

        Response.Headers.CacheControl = "public, max-age=60";

        return Ok(new AppConfigResponse(
            new AppConfigMaintenanceResponse(config.Maintenance.Enabled),
            new AppConfigVersionsResponse(
                ToPlatformResponse(config.Versions.Ios),
                ToPlatformResponse(config.Versions.Android)),
            new AppConfigFeaturesResponse(
                config.Features.AiRecommendations,
                config.Features.ReviewTranslation)));
    }

    private static AppConfigPlatformVersionResponse ToPlatformResponse(
        MobileAppConfigPlatformVersionOptions platform) =>
        new(
            platform.MinimumBuild,
            platform.LatestBuild,
            platform.StoreUrl.Trim());
}
