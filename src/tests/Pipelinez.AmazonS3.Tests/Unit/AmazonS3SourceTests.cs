using System.Text;
using Amazon.S3.Model;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Source;
using Pipelinez.AmazonS3.Tests.Infrastructure;
using Pipelinez.AmazonS3.Tests.Models;
using Pipelinez.Core;
using Xunit;

namespace Pipelinez.AmazonS3.Tests.Unit;

public class AmazonS3SourceTests
{
    [Fact]
    public async Task Source_Publishes_Object_And_Deletes_After_Success()
    {
        var factory = new FakeAmazonS3ClientFactory();
        factory.Client.EnqueueListResponse(new ListObjectsV2Response
        {
            S3Objects =
            [
                new S3Object
                {
                    BucketName = "orders",
                    Key = "incoming/A-100.txt",
                    ETag = "\"abc\"",
                    Size = 7,
                    LastModified = DateTime.UtcNow
                }
            ]
        });
        factory.Client.AddObject("incoming/A-100.txt", "A-100|ok", versionId: "version-1");

        var source = new AmazonS3PipelineSource<TestAmazonS3Record>(
            new AmazonS3SourceOptions
            {
                Bucket = new AmazonS3BucketOptions
                {
                    BucketName = "orders",
                    Prefix = "incoming/"
                },
                Settlement = new AmazonS3ObjectSettlementOptions
                {
                    OnSuccess = AmazonS3ObjectSettlementAction.Delete
                }
            },
            MapRecordAsync,
            factory);
        var destination = new CollectingDestination<TestAmazonS3Record>();
        var pipeline = Pipeline<TestAmazonS3Record>.New("s3-source-delete")
            .WithSource(source)
            .WithDestination(destination)
            .Build();

        await pipeline.StartPipelineAsync();
        var record = await WaitForRecordAsync(destination);
        await pipeline.CompleteAsync();

        Assert.Equal("A-100", record.Id);
        Assert.Equal("ok", record.Value);
        var delete = Assert.Single(factory.Client.DeleteObjectRequests);
        Assert.Equal("orders", delete.BucketName);
        Assert.Equal("incoming/A-100.txt", delete.Key);
        Assert.Equal("version-1", delete.VersionId);
    }

    [Fact]
    public async Task Source_Moves_Object_After_Success()
    {
        var factory = new FakeAmazonS3ClientFactory();
        factory.Client.EnqueueListResponse(new ListObjectsV2Response
        {
            S3Objects =
            [
                new S3Object
                {
                    BucketName = "orders",
                    Key = "incoming/A-200.txt",
                    Size = 8,
                    LastModified = DateTime.UtcNow
                }
            ]
        });
        factory.Client.AddObject("incoming/A-200.txt", "A-200|ok");

        var source = new AmazonS3PipelineSource<TestAmazonS3Record>(
            new AmazonS3SourceOptions
            {
                Bucket = new AmazonS3BucketOptions
                {
                    BucketName = "orders",
                    Prefix = "incoming/"
                },
                Settlement = new AmazonS3ObjectSettlementOptions
                {
                    OnSuccess = AmazonS3ObjectSettlementAction.Move,
                    SuccessPrefix = "processed/"
                }
            },
            MapRecordAsync,
            factory);
        var pipeline = Pipeline<TestAmazonS3Record>.New("s3-source-move")
            .WithSource(source)
            .WithDestination(new CollectingDestination<TestAmazonS3Record>())
            .Build();

        await pipeline.StartPipelineAsync();
        await WaitForSettlementAsync(() => factory.Client.CopyObjectRequests.Count > 0);
        await pipeline.CompleteAsync();

        var copy = Assert.Single(factory.Client.CopyObjectRequests);
        Assert.Equal("incoming/A-200.txt", copy.SourceKey);
        Assert.Equal("processed/A-200.txt", copy.DestinationKey);
        var delete = Assert.Single(factory.Client.DeleteObjectRequests);
        Assert.Equal("incoming/A-200.txt", delete.Key);
    }

    [Fact]
    public async Task Source_Loads_Object_Tags_When_Configured()
    {
        var factory = new FakeAmazonS3ClientFactory();
        factory.Client.EnqueueListResponse(new ListObjectsV2Response
        {
            S3Objects =
            [
                new S3Object
                {
                    BucketName = "orders",
                    Key = "incoming/A-300.txt",
                    Size = 8,
                    LastModified = DateTime.UtcNow
                }
            ]
        });
        factory.Client.AddObject("incoming/A-300.txt", "A-300|vip");
        factory.Client.AddTags("incoming/A-300.txt", new Tag { Key = "tier", Value = "gold" });

        string? observedTier = null;
        var source = new AmazonS3PipelineSource<TestAmazonS3Record>(
            new AmazonS3SourceOptions
            {
                Bucket = new AmazonS3BucketOptions
                {
                    BucketName = "orders",
                    Prefix = "incoming/"
                },
                LoadObjectTags = true
            },
            async (context, token) =>
            {
                observedTier = context.Tags["tier"];
                return await MapRecordAsync(context, token);
            },
            factory);
        var pipeline = Pipeline<TestAmazonS3Record>.New("s3-source-tags")
            .WithSource(source)
            .WithDestination(new CollectingDestination<TestAmazonS3Record>())
            .Build();

        await pipeline.StartPipelineAsync();
        await WaitForSettlementAsync(() => observedTier is not null);
        await pipeline.CompleteAsync();

        Assert.Equal("gold", observedTier);
    }

    private static async Task<TestAmazonS3Record> MapRecordAsync(
        AmazonS3ObjectContext context,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Content, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var parts = text.Split('|', 2);
        return new TestAmazonS3Record { Id = parts[0], Value = parts[1] };
    }

    private static async Task<TestAmazonS3Record> WaitForRecordAsync(
        CollectingDestination<TestAmazonS3Record> destination)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (destination.Records.Count > 0)
            {
                return Assert.Single(destination.Records);
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Timed out waiting for the Amazon S3 source to publish a record.");
    }

    private static async Task WaitForSettlementAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Timed out waiting for the Amazon S3 source to settle an object.");
    }
}
