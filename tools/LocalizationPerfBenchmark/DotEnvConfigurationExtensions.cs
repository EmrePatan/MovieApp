using Microsoft.Extensions.Configuration;

namespace MovieApp.LocalizationPerfBenchmark;

internal static class DotEnvConfigurationExtensions
{
    public static void AddDotEnvFile(IConfigurationBuilder builder, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        if (values.TryGetValue("POSTGRES_PASSWORD", out var postgresPassword) &&
            !values.ContainsKey("PostgreSql:Password"))
        {
            values["PostgreSql:Password"] = postgresPassword;
        }

        builder.AddInMemoryCollection(values);
    }
}
