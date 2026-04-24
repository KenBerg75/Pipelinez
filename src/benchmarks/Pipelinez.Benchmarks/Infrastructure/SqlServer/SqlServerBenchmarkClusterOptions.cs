namespace Pipelinez.Benchmarks;

internal sealed class SqlServerBenchmarkClusterOptions
{
    public const string DefaultImage = "mcr.microsoft.com/mssql/server:2022-latest";
    public const string DefaultPassword = "P@ssw0rd!2026";
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromMinutes(3);

    public string Image { get; init; } = DefaultImage;
    public string Password { get; init; } = DefaultPassword;
    public TimeSpan StartupTimeout { get; init; } = DefaultStartupTimeout;
    public bool ReuseContainer { get; init; }

    public static SqlServerBenchmarkClusterOptions LoadFromEnvironment()
    {
        return new SqlServerBenchmarkClusterOptions
        {
            Image = GetEnvironmentValue("PIPELINEZ_BENCH_SQLSERVER_IMAGE", "PIPELINEZ_SQLSERVER_TEST_IMAGE", DefaultImage),
            Password = GetEnvironmentValue("PIPELINEZ_BENCH_SQLSERVER_PASSWORD", "PIPELINEZ_SQLSERVER_TEST_PASSWORD", DefaultPassword),
            StartupTimeout = GetEnvironmentSeconds("PIPELINEZ_BENCH_SQLSERVER_STARTUP_TIMEOUT_SECONDS", "PIPELINEZ_SQLSERVER_TEST_STARTUP_TIMEOUT_SECONDS", DefaultStartupTimeout),
            ReuseContainer = GetEnvironmentBool("PIPELINEZ_BENCH_SQLSERVER_REUSE_CONTAINER", "PIPELINEZ_SQLSERVER_TEST_REUSE_CONTAINER")
        };
    }

    private static string GetEnvironmentValue(string preferredVariable, string fallbackVariable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(preferredVariable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = Environment.GetEnvironmentVariable(fallbackVariable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static TimeSpan GetEnvironmentSeconds(string preferredVariable, string fallbackVariable, TimeSpan fallback)
    {
        var value = Environment.GetEnvironmentVariable(preferredVariable)
                    ?? Environment.GetEnvironmentVariable(fallbackVariable);

        return int.TryParse(value, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : fallback;
    }

    private static bool GetEnvironmentBool(string preferredVariable, string fallbackVariable)
    {
        var value = Environment.GetEnvironmentVariable(preferredVariable)
                    ?? Environment.GetEnvironmentVariable(fallbackVariable);

        return bool.TryParse(value, out var parsed) && parsed;
    }
}
