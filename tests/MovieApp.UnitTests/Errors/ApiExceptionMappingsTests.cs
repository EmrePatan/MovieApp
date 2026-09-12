using Microsoft.AspNetCore.Http;
using MovieApp.Api.Errors;
using MovieApp.Application.Exceptions;

namespace MovieApp.UnitTests.Errors;

public sealed class ApiExceptionMappingsTests
{
    [Fact]
    public void TryMapValidationExceptionTo400()
    {
        var mapped = ApiExceptionMappings.TryMap(
            new ValidationException("Email is required."),
            out var mapping);

        Assert.True(mapped);
        Assert.Equal(StatusCodes.Status400BadRequest, mapping.Status);
        Assert.Equal(ApiErrorCodes.ValidationFailed, mapping.Code);
    }

    [Fact]
    public void TryMapAuthenticationExceptionTo401()
    {
        var mapped = ApiExceptionMappings.TryMap(
            new AuthenticationException("Authentication is required."),
            out var mapping);

        Assert.True(mapped);
        Assert.Equal(StatusCodes.Status401Unauthorized, mapping.Status);
        Assert.Equal(ApiErrorCodes.AuthenticationFailed, mapping.Code);
    }

    [Fact]
    public void TryMapNotFoundExceptionTo404()
    {
        var mapped = ApiExceptionMappings.TryMap(
            new NotFoundException("Missing resource."),
            out var mapping);

        Assert.True(mapped);
        Assert.Equal(StatusCodes.Status404NotFound, mapping.Status);
        Assert.Equal(ApiErrorCodes.NotFound, mapping.Code);
    }

    [Fact]
    public void TryMapConflictExceptionTo409()
    {
        var mapped = ApiExceptionMappings.TryMap(
            new ConflictException("Already exists."),
            out var mapping);

        Assert.True(mapped);
        Assert.Equal(StatusCodes.Status409Conflict, mapping.Status);
        Assert.Equal(ApiErrorCodes.Conflict, mapping.Code);
    }

    [Fact]
    public void FromStatusCodeMaps403ToForbidden()
    {
        var mapping = ApiExceptionMappings.FromStatusCode(StatusCodes.Status403Forbidden);

        Assert.Equal(StatusCodes.Status403Forbidden, mapping.Status);
        Assert.Equal(ApiErrorCodes.Forbidden, mapping.Code);
    }

    [Fact]
    public void TryMapUnknownExceptionReturnsFalse()
    {
        var mapped = ApiExceptionMappings.TryMap(
            new InvalidOperationException("secret"),
            out _);

        Assert.False(mapped);
    }
}
