namespace MovieApp.Infrastructure.Providers.MdbList;

public sealed class MdbListTitleResponseJson
{
    public List<MdbListRatingJson>? Ratings { get; set; }
}

public sealed class MdbListRatingJson
{
    public string? Source { get; set; }

    public decimal? Value { get; set; }

    public long? Votes { get; set; }
}
