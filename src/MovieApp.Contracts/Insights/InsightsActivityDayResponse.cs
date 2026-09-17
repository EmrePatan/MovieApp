namespace MovieApp.Contracts.Insights;

public sealed record InsightsActivityDayResponse(
    DateOnly Date,
    int Movies,
    int Episodes,
    int Total,
    InsightsActivityDayStateResponse State,
    int IntensityBucket);
