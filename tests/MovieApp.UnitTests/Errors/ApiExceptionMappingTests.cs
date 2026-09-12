using Microsoft.AspNetCore.Http;
using MovieApp.Api.Errors;
using MovieApp.Application.Exceptions;

namespace MovieApp.UnitTests.Errors;

public sealed class ApiExceptionMappingTests
{
    [Fact]
    public void MapsSearchProviderUnavailableTo503()
    {
        var mapped = ApiExceptionMappings.TryMap(new SearchProviderUnavailableException(), out var mapping);

        Assert.True(mapped);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, mapping.Status);
        Assert.Equal(ApiErrorCodes.SearchProviderUnavailable, mapping.Code);
        Assert.Equal("Search provider is temporarily unavailable.", mapping.Detail);
    }
}
