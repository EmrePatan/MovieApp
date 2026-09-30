using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class KeywordGraphOptionsValidator : IValidateOptions<KeywordGraphOptions>
{
    public ValidateOptionsResult Validate(string? name, KeywordGraphOptions options) =>
        ValidateOptionsResult.Success;
}
