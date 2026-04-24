namespace Pipelinez.Benchmarks;

public abstract class BenchmarkSuiteBase
{
    [BenchmarkDotNet.Attributes.Params(100, 1_000)]
    public int RecordCount { get; set; }

    [BenchmarkDotNet.Attributes.Params(PayloadProfile.SmallJson, PayloadProfile.MediumJson)]
    public PayloadProfile PayloadProfile { get; set; }

    protected TimeSpan ObservationTimeout => BenchmarkProfiles.ObservationTimeout;

    protected IReadOnlyList<BenchmarkRecord> CreateSuccessfulRecords()
    {
        return CreateRecords(shouldFault: false);
    }

    protected IReadOnlyList<BenchmarkRecord> CreateFaultingRecords()
    {
        return CreateRecords(shouldFault: true);
    }

    protected string CreateScenarioName(string suffix)
    {
        return BenchmarkScenarioNameFactory.CreateQueueName(GetType().Name, suffix);
    }

    private IReadOnlyList<BenchmarkRecord> CreateRecords(bool shouldFault)
    {
        var records = new List<BenchmarkRecord>(RecordCount);

        for (var index = 0; index < RecordCount; index++)
        {
            records.Add(
                new BenchmarkRecord
                {
                    Id = $"record-{index:D6}",
                    Payload = BenchmarkPayloadFactory.CreatePayload(PayloadProfile, index),
                    ShouldFault = shouldFault
                });
        }

        return records;
    }
}
