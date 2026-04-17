using System.Text;
using DotNet.Testcontainers.Builders;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.AmazonS3.Source;
using Pipelinez.AmazonS3.Tests.Infrastructure;
using Pipelinez.AmazonS3.Tests.Models;
using Pipelinez.Core;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Segment;
using Xunit;

namespace Pipelinez.AmazonS3.Tests.EndToEnd;

public class AmazonS3PipelineIntegrationTests
{
    [Fact]
    public async Task Destination_Writes_Object_To_S3()
    {
        await using var cluster = await TryStartClusterAsync();
        if (cluster is null)
        {
            return;
        }

        var bucketName = CreateBucketName("destination");
        await cluster.CreateBucketAsync(bucketName);

        var pipeline = Pipeline<TestAmazonS3Record>.New("s3-destination-integration")
            .WithInMemorySource(new object())
            .WithAmazonS3Destination(
                new AmazonS3DestinationOptions
                {
                    Connection = cluster.CreateConnectionOptions(),
                    BucketName = bucketName,
                    Write = new AmazonS3ObjectWriteOptions { KeyPrefix = "processed/" }
                },
                record => AmazonS3PutObject.FromText($"{record.Id}.txt", record.Value, "text/plain"))
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestAmazonS3Record { Id = "A-100", Value = "hello" });
        await pipeline.CompleteAsync();

        var content = await cluster.WaitForObjectContentAsync(bucketName, "processed/A-100.txt");
        Assert.Equal("hello", content);
    }

    [Fact]
    public async Task Source_Reads_Object_And_Moves_After_Success()
    {
        await using var cluster = await TryStartClusterAsync();
        if (cluster is null)
        {
            return;
        }

        var bucketName = CreateBucketName("source");
        await cluster.CreateBucketAsync(bucketName);
        await cluster.PutObjectAsync(bucketName, "incoming/A-200.txt", "A-200|ready");
        Assert.Equal("A-200|ready", await cluster.WaitForObjectContentAsync(bucketName, "incoming/A-200.txt"));

        var destination = new CollectingDestination<TestAmazonS3Record>();
        var pipeline = Pipeline<TestAmazonS3Record>.New("s3-source-integration")
            .WithAmazonS3Source(
                new AmazonS3SourceOptions
                {
                    Connection = cluster.CreateConnectionOptions(),
                    Bucket = new AmazonS3BucketOptions
                    {
                        BucketName = bucketName,
                        Prefix = "incoming/"
                    },
                    Settlement = new AmazonS3ObjectSettlementOptions
                    {
                        OnSuccess = AmazonS3ObjectSettlementAction.Move,
                        SuccessPrefix = "processed/"
                    }
                },
                MapRecordAsync)
            .WithDestination(destination)
            .Build();

        await pipeline.StartPipelineAsync();
        var record = await WaitForRecordAsync(destination);
        await pipeline.CompleteAsync();

        Assert.Equal("A-200", record.Id);
        Assert.Equal("ready", record.Value);
        Assert.False(await cluster.ObjectExistsAsync(bucketName, "incoming/A-200.txt"));
        Assert.Equal("A-200|ready", await cluster.WaitForObjectContentAsync(bucketName, "processed/A-200.txt"));
    }

    [Fact]
    public async Task DeadLetterDestination_Writes_Faulted_Record_To_S3()
    {
        await using var cluster = await TryStartClusterAsync();
        if (cluster is null)
        {
            return;
        }

        var bucketName = CreateBucketName("deadletter");
        await cluster.CreateBucketAsync(bucketName);

        var pipeline = Pipeline<TestAmazonS3Record>.New("s3-deadletter-integration")
            .WithInMemorySource(new object())
            .AddSegment(new FaultingSegment(), new object())
            .WithInMemoryDestination("memory")
            .WithAmazonS3DeadLetterDestination(new AmazonS3DeadLetterOptions
            {
                Connection = cluster.CreateConnectionOptions(),
                BucketName = bucketName,
                KeyPrefix = "dead-letter/"
            })
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestAmazonS3Record { Id = "A-300", Value = "bad" });
        await pipeline.CompleteAsync();

        var objects = await cluster.ListObjectsAsync(bucketName, "dead-letter/");
        var deadLetterObject = Assert.Single(objects);
        var content = await cluster.GetObjectContentAsync(bucketName, deadLetterObject.Key);
        Assert.Contains("\"id\":\"A-300\"", content, StringComparison.Ordinal);
    }

    private static async Task<TestAmazonS3Record> MapRecordAsync(
        AmazonS3ObjectContext context,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Content, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var parts = text.Split('|', 2);
        return new TestAmazonS3Record { Id = parts[0], Value = parts[1] };
    }

    private static string CreateBucketName(string suffix)
    {
        return $"pipelinez-s3-{suffix}-{Guid.NewGuid():N}"[..48];
    }

    private static async Task<TestAmazonS3Record> WaitForRecordAsync(
        CollectingDestination<TestAmazonS3Record> destination)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (destination.Records.Count > 0)
            {
                return Assert.Single(destination.Records);
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for the Amazon S3 source to publish a record.");
    }

    private static async Task<AmazonS3TestCluster?> TryStartClusterAsync()
    {
        try
        {
            var cluster = new AmazonS3TestCluster();
            await cluster.InitializeAsync().ConfigureAwait(false);
            return cluster;
        }
        catch (DockerUnavailableException)
        {
            return null;
        }
    }

    private sealed class FaultingSegment : PipelineSegment<TestAmazonS3Record>
    {
        public override Task<TestAmazonS3Record> ExecuteAsync(TestAmazonS3Record arg)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
