using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.ExternalRatings;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Providers.MdbList;

namespace MovieApp.Infrastructure.ExternalRatings;

public sealed class ExternalRatingsFeatureState(
    IOptions<ExternalRatingsOptions> externalRatingsOptions,
    IOptions<MdbListOptions> mdbListOptions) : IExternalRatingsFeatureState
{
    public bool IsOperational =>
        externalRatingsOptions.Value.IsOperational(mdbListOptions.Value.ApiKey);
}
