namespace MovieApp.Application.Configuration;

public sealed class HomeOptions
{
    public const string SectionName = "Home";

    public int DefaultSectionSize { get; set; } = 10;

    public int HeroSectionSize { get; set; } = 5;

    public int ComingUpSectionSize { get; set; } = 5;

    public int MaximumSectionSize { get; set; } = 20;

    public int HotThisWeekCacheTtlMinutes { get; set; } = 30;

    public int CacheTtlMinutes { get; set; } = 5;

    public List<string> GenreSections { get; set; } = ["Science Fiction", "Action", "Drama", "Comedy"];
}
