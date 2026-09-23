using Microsoft.AspNetCore.Http;
using MovieApp.Api.Observability;
using Serilog.Events;

namespace MovieApp.UnitTests.Observability;

public sealed class RequestLoggingLevelPolicyTests
{
    [Fact]
    public void GetLevel_DowngradesSuccessfulHealthLiveProbeToDebug()
    {
        var context = CreateContext("/health/live", StatusCodes.Status200OK);

        var level = RequestLoggingLevelPolicy.GetLevel(context, null);

        Assert.Equal(LogEventLevel.Debug, level);
    }

    [Fact]
    public void GetLevel_KeepsFailedHealthLiveProbeAtError()
    {
        var context = CreateContext("/health/live", StatusCodes.Status500InternalServerError);

        var level = RequestLoggingLevelPolicy.GetLevel(context, null);

        Assert.Equal(LogEventLevel.Error, level);
    }

    [Fact]
    public void GetLevel_KeepsHealthReadyProbeAtInformation()
    {
        var context = CreateContext("/health/ready", StatusCodes.Status200OK);

        var level = RequestLoggingLevelPolicy.GetLevel(context, null);

        Assert.Equal(LogEventLevel.Information, level);
    }

    [Fact]
    public void GetLevel_KeepsApiRequestsAtInformation()
    {
        var context = CreateContext("/api/movies/123", StatusCodes.Status200OK);

        var level = RequestLoggingLevelPolicy.GetLevel(context, null);

        Assert.Equal(LogEventLevel.Information, level);
    }

    private static DefaultHttpContext CreateContext(string path, int statusCode)
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = path,
            },
            Response =
            {
                StatusCode = statusCode,
            },
        };

        return context;
    }
}
