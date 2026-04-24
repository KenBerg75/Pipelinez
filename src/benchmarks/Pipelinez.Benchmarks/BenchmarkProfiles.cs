namespace Pipelinez.Benchmarks;

public enum PayloadProfile
{
    SmallJson,
    MediumJson
}

internal static class BenchmarkProfiles
{
    public static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
}
