using System.Reflection;

namespace MovieApp.Application;

public static class ApplicationSourceVersion
{
    public const string EnvironmentVariableName = "MOVIEAPP_SOURCE_VERSION";

    public static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (IsUsableVersion(fromEnvironment))
        {
            return fromEnvironment!;
        }

        var entryAssemblyVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (IsUsableVersion(entryAssemblyVersion))
        {
            return entryAssemblyVersion!;
        }

        var executingAssemblyVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (IsUsableVersion(executingAssemblyVersion))
        {
            return executingAssemblyVersion!;
        }

        return "unknown";
    }

    private static bool IsUsableVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase);
}
