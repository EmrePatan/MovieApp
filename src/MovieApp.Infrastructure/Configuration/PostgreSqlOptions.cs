namespace MovieApp.Infrastructure.Configuration;

public sealed class PostgreSqlOptions
{
    public const string SectionName = "PostgreSql";

    public string ConnectionString { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool IsConfigured()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(Host)
            && !string.IsNullOrWhiteSpace(Database)
            && !string.IsNullOrWhiteSpace(Username);
    }

    public string ResolveConnectionString()
    {
        var connectionString = !string.IsNullOrWhiteSpace(ConnectionString)
            ? ConnectionString
            : $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";

        return PostgreSqlConnectionStringFactory.Normalize(connectionString);
    }
}
