namespace MovieApp.Application.Configuration;

public sealed class ReleaseRegionOptions
{
    public const string SectionName = "ReleaseRegion";

    public string DefaultRegion { get; set; } = "TR";
}
