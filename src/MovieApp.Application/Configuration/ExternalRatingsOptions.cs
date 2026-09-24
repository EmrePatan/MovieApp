namespace MovieApp.Application.Configuration;

public sealed class ExternalRatingsOptions
{
    public const string SectionName = "ExternalRatings";

    public bool Enabled { get; set; }

    public int FreshHours { get; set; } = 72;

    public int StaleDays { get; set; } = 14;

    public int NegativeHours { get; set; } = 24;

    public bool IsOperational(string? mdbListApiKey) =>
        Enabled && !string.IsNullOrWhiteSpace(mdbListApiKey);
}
