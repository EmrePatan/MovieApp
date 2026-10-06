using MovieApp.Application.Models.Search;

namespace MovieApp.Api.Mapping;

internal static class DiscoveryBatchContractStatus
{
    internal const string Ok = "ok";
    internal const string TransientError = "transient_error";

    internal static string ToContract(DiscoveryBatchItemStatus status) =>
        status switch
        {
            DiscoveryBatchItemStatus.Ok => Ok,
            DiscoveryBatchItemStatus.TransientError => TransientError,
            _ => TransientError,
        };
}
