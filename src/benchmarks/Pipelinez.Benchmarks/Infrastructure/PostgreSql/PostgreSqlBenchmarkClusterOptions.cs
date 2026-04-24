namespace Pipelinez.Benchmarks;

internal sealed class PostgreSqlBenchmarkClusterOptions
{
    public const string DefaultImage = "postgres:16-alpine";
    public const string DefaultDatabase = "pipelinez_benchmarks";
    public const string DefaultUsername = "postgres";
    public const string DefaultPassword = "postgres";
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromMinutes(2);

    public string Image { get; init; } = DefaultImage;
    public string Database { get; init; } = DefaultDatabase;
    public string Username { get; init; } = DefaultUsername;
    public string Password { get; init; } = DefaultPassword;
    public TimeSpan StartupTimeout { get; init; } = DefaultStartupTimeout;
    public bool ReuseContainer { get; init; }

    public static PostgreSqlBenchmarkClusterOptions LoadFromEnvironment()
    {
        return new PostgreSqlBenchmarkClusterOptions
        {
            Image = GetEnvironmentValue("PIPELINEZ_BENCH_POSTGRESQL_IMAGE", "PIPELINEZ_POSTGRESQL_TEST_IMAGE", DefaultImage),
            Database = GetEnvironmentValue("PIPELINEZ_BENCH_POSTGRESQL_DATABASE", "PIPELINEZ_POSTGRESQL_TEST_DATABASE", DefaultDatabase),
            Username = GetEnvironmentValue("PIPELINEZ_BENCH_POSTGRESQL_USERNAME", "PIPELINEZ_POSTGRESQL_TEST_USERNAME", DefaultUsername),
            Password = GetEnvironmentValue("PIPELINEZ_BENCH_POSTGRESQL_PASSWORD", "PIPELINEZ_POSTGRESQL_TEST_PASSWORD", DefaultPassword),
            StartupTimeout = GetEnvironmentSeconds("PIPELINEZ_BENCH_POSTGRESQL_STARTUP_TIMEOUT_SECONDS", "PIPELINEZ_POSTGRESQL_TEST_STARTUP_TIMEOUT_SECONDS", DefaultStartupTimeout),
            ReuseContainer = GetEnvironmentBool("PIPELINEZ_BENCH_POSTGRESQL_REUSE_CONTAINER", "PIPELINEZ_POSTGRESQL_TEST_REUSE_CONTAINER")
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
