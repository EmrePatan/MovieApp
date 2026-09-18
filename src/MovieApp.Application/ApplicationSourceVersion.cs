using System.Reflection;

namespace MovieApp.Application;

public static class ApplicationSourceVersion
{
    public const string EnvironmentVariableName = "MOVIEAPP_SOURCE_VERSION";

    public static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "unknown";
    }
}
