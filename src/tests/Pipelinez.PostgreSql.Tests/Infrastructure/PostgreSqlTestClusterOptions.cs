namespace Pipelinez.PostgreSql.Tests.Infrastructure;

public sealed class PostgreSqlTestClusterOptions
{
    public const string DefaultImage = "postgres:16-alpine";
    public const string DefaultDatabase = "pipelinez_tests";
    public const string DefaultUsername = "postgres";
    public const string DefaultPassword = "postgres";
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromMinutes(2);

    public string Image { get; init; } = DefaultImage;
    public string Database { get; init; } = DefaultDatabase;
    public string Username { get; init; } = DefaultUsername;
    public string Password { get; init; } = DefaultPassword;
    public TimeSpan StartupTimeout { get; init; } = DefaultStartupTimeout;
    public bool ReuseContainer { get; init; }

    public static PostgreSqlTestClusterOptions LoadFromEnvironment()
    {
        return new PostgreSqlTestClusterOptions
        {
            Image = GetEnvironmentValue("PIPELINEZ_POSTGRESQL_TEST_IMAGE", DefaultImage),
            Database = GetEnvironmentValue("PIPELINEZ_POSTGRESQL_TEST_DATABASE", DefaultDatabase),
            Username = GetEnvironmentValue("PIPELINEZ_POSTGRESQL_TEST_USERNAME", DefaultUsername),
            Password = GetEnvironmentValue("PIPELINEZ_POSTGRESQL_TEST_PASSWORD", DefaultPassword),
            StartupTimeout = GetEnvironmentSeconds("PIPELINEZ_POSTGRESQL_TEST_STARTUP_TIMEOUT_SECONDS", DefaultStartupTimeout),
            ReuseContainer = GetEnvironmentBool("PIPELINEZ_POSTGRESQL_TEST_REUSE_CONTAINER")
        };
    }

    private static string GetEnvironmentValue(string variableName, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static TimeSpan GetEnvironmentSeconds(string variableName, TimeSpan fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return int.TryParse(value, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : fallback;
    }

    private static bool GetEnvironmentBool(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return bool.TryParse(value, out var parsed) && parsed;
    }
}
