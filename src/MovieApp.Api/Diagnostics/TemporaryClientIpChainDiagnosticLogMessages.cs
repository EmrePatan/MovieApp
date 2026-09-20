namespace MovieApp.Api.Diagnostics;

// TEMPORARY (#27 M4): remove after Render client-IP chain verification.
internal static partial class TemporaryClientIpChainDiagnosticLogMessages
{
    [LoggerMessage(
        EventId = 27_04_001,
        Level = LogLevel.Information,
        Message = "TEMPORARY Client IP chain diagnostic. RemoteIpAddress={RemoteIpAddress} XForwardedForHopCount={XForwardedForHopCount} XForwardedForChain={XForwardedForChain} CfConnectingIp={CfConnectingIp} CfRay={CfRay} Path={Path}")]
    public static partial void LogClientIpChain(
        ILogger logger,
        string? remoteIpAddress,
        int xForwardedForHopCount,
        string[] xForwardedForChain,
        string? cfConnectingIp,
        string? cfRay,
        string path);
}
