using System.Text;

namespace Pipelinez.Benchmarks;

internal static class BenchmarkPayloadFactory
{
    private static readonly string SmallContent = BuildRepeatedContent(900);
    private static readonly string MediumContent = BuildRepeatedContent(32_000);

    public static string CreatePayload(PayloadProfile profile, int seed)
    {
        var content = profile switch
        {
            PayloadProfile.SmallJson => SmallContent,
            PayloadProfile.MediumJson => MediumContent,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown payload profile.")
        };

        return $$"""{"recordId":"record-{{seed:D6}}","profile":"{{profile}}","content":"{{content}}"}""";
    }

    private static string BuildRepeatedContent(int targetLength)
    {
        const string token = "pipelinez-benchmark-data-";
        var builder = new StringBuilder(targetLength);

        while (builder.Length < targetLength)
        {
            builder.Append(token);
        }

        return builder.ToString(0, targetLength);
    }
}
