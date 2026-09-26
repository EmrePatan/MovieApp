using System.Diagnostics;

namespace MovieApp.Infrastructure.Performance;

internal static class RuntimePerfSnapshot
{
    internal static RuntimePerfValues Capture()
    {
        ThreadPool.GetAvailableThreads(out var workerAvailable, out var ioAvailable);
        ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);
        ThreadPool.GetMinThreads(out var workerMin, out var ioMin);

        return new RuntimePerfValues(
            ThreadPool.ThreadCount,
            workerAvailable,
            workerMax,
            workerMin,
            ioAvailable,
            ioMax,
            ioMin,
            ThreadPool.PendingWorkItemCount,
            GC.GetTotalMemory(false) / (1024 * 1024),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024));
    }
}

internal readonly record struct RuntimePerfValues(
    int ThreadPoolThreadCount,
    int WorkerAvailable,
    int WorkerMax,
    int WorkerMin,
    int IoAvailable,
    int IoMax,
    int IoMin,
    long PendingWorkItemCount,
    long GcHeapMb,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long WorkingSetMb);
