using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class NowInTheatersOptionsValidator : IValidateOptions<NowInTheatersOptions>
{
    public ValidateOptionsResult Validate(string? name, NowInTheatersOptions options)
    {
        if (options.MaxAgeDays < 0)
        {
            return ValidateOptionsResult.Fail(
                "NowInTheaters:MaxAgeDays must be greater than or equal to 0.");
        }

        return ValidateOptionsResult.Success;
    }
}
