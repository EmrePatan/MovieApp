namespace MovieApp.Application.Caching;

public sealed class ProfileStatisticsGenerationState
{
    public long Value { get; init; }

    public ProfileStatisticsGenerationState(long value)
    {
        Value = value;
    }
}
