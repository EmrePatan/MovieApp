namespace MovieApp.Api.Diagnostics;

// TEMPORARY (#27 M4): remove after Render client-IP chain verification.
internal sealed class TemporaryClientIpChainDiagnosticOptions
{
    internal const string SectionName = "Diagnostics:ClientIpChainLogging";

    public bool Enabled { get; set; }
}
