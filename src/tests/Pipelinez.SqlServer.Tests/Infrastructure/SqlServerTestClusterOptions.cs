namespace Pipelinez.SqlServer.Tests.Infrastructure;

public sealed class SqlServerTestClusterOptions
{
    public const string DefaultImage = "mcr.microsoft.com/mssql/server:2022-latest";
    public const string DefaultPassword = "P@ssw0rd!2026";
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromMinutes(3);

    public string Image { get; init; } = DefaultImage;
    public string Password { get; init; } = DefaultPassword;
    public TimeSpan StartupTimeout { get; init; } = DefaultStartupTimeout;
    public bool ReuseContainer { get; init; }

    public static SqlServerTestClusterOptions LoadFromEnvironment()
    {
        return new SqlServerTestClusterOptions
        {
            Image = GetEnvironmentValue("PIPELINEZ_SQLSERVER_TEST_IMAGE", DefaultImage),
            Password = GetEnvironmentValue("PIPELINEZ_SQLSERVER_TEST_PASSWORD", DefaultPassword),
            StartupTimeout = GetEnvironmentSeconds("PIPELINEZ_SQLSERVER_TEST_STARTUP_TIMEOUT_SECONDS", DefaultStartupTimeout),
            ReuseContainer = GetEnvironmentBool("PIPELINEZ_SQLSERVER_TEST_REUSE_CONTAINER")
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
