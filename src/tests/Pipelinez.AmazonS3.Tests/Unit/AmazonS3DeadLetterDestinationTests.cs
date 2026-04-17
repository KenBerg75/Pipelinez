using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.DeadLettering;
using Pipelinez.AmazonS3.Tests.Infrastructure;
using Pipelinez.AmazonS3.Tests.Models;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record.Metadata;
using Xunit;

namespace Pipelinez.AmazonS3.Tests.Unit;

public class AmazonS3DeadLetterDestinationTests
{
    [Fact]
    public async Task DeadLetterDestination_Writes_Default_Json_Envelope()
    {
        var factory = new FakeAmazonS3ClientFactory();
        var destination = new AmazonS3DeadLetterDestination<TestAmazonS3Record>(
            new AmazonS3DeadLetterOptions
            {
                BucketName = "deadletters",
                KeyPrefix = "dlq/"
            },
            factory);
        var metadata = new MetadataCollection();
        metadata.Set("pipelinez.amazon-s3.object-key", "incoming/A-500.txt");

        await destination.WriteAsync(
            new PipelineDeadLetterRecord<TestAmazonS3Record>
            {
                Record = new TestAmazonS3Record { Id = "A-500", Value = "bad" },
                Fault = new PipelineFaultState(
                    new InvalidOperationException("boom"),
                    "Destination",
                    PipelineComponentKind.Destination,
                    DateTimeOffset.UtcNow,
                    "boom"),
                Metadata = metadata,
                SegmentHistory = [],
                RetryHistory = [],
                CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
                DeadLetteredAtUtc = DateTimeOffset.UtcNow,
                Distribution = new PipelineRecordDistributionContext(
                    "instance-1",
                    "worker-1",
                    "AmazonS3",
                    "lease-1",
                    "orders",
                    null,
                    null)
            },
            CancellationToken.None);

        var request = Assert.Single(factory.Client.PutObjectRequests);
        Assert.Equal("deadletters", request.BucketName);
        Assert.StartsWith("dlq/", request.Key, StringComparison.Ordinal);
        Assert.EndsWith(".json", request.Key, StringComparison.Ordinal);
        Assert.Equal("application/json", request.ContentType);
        Assert.Contains("\"id\":\"A-500\"", request.ContentBody, StringComparison.Ordinal);
        Assert.Equal("AmazonS3", request.Metadata["pipelinez-pipeline-source-transport"]);
    }
}
