namespace MovieApp.Infrastructure.Configuration;

public sealed class ForwardedHeadersOptionsConfig
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }

    public string[] KnownProxies { get; set; } = [];

    public string[] KnownNetworks { get; set; } = [];
}
