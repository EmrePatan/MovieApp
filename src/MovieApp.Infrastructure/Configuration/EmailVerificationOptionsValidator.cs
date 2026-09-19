using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Configuration;

public sealed class EmailVerificationOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<EmailVerificationOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailVerificationOptions options)
    {
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:EmailVerification:BaseUrl is required in production.");
        }

        return ValidateOptionsResult.Success;
    }
}
