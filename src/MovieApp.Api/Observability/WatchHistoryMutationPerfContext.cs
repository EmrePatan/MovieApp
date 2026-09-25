namespace MovieApp.Api.Observability;

internal static class WatchHistoryMutationPerfContext
{
    internal const string PipelineStartTicksKey = "WatchHistory.PipelineStartTicks";
    internal const string PreServiceMsKey = "WatchHistory.PreServiceMs";
    internal const string ServiceMsKey = "WatchHistory.ServiceMs";
}
