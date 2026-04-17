using System.Text;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.AmazonS3.Tests.Infrastructure;
using Pipelinez.AmazonS3.Tests.Models;
using Pipelinez.Core;
using Xunit;

namespace Pipelinez.AmazonS3.Tests.Unit;

public class AmazonS3DestinationTests
{
    [Fact]
    public async Task Destination_Publishes_Mapped_Object()
    {
        var factory = new FakeAmazonS3ClientFactory();
        var destination = new AmazonS3PipelineDestination<TestAmazonS3Record>(
            CreateDestinationOptions(),
            record => new AmazonS3PutObject
            {
                Key = $"{record.Id}.json",
                TextContent = record.Value,
                ContentType = "application/json",
                Metadata = new Dictionary<string, string> { ["tenant"] = "north" },
                Tags = new Dictionary<string, string> { ["kind"] = "order" }
            },
            factory);

        var pipeline = Pipeline<TestAmazonS3Record>.New("s3-destination")
            .WithInMemorySource(new object())
            .WithDestination(destination)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestAmazonS3Record { Id = "A-100", Value = "{\"ok\":true}" });
        await pipeline.CompleteAsync();

        var request = Assert.Single(factory.Client.PutObjectRequests);
        Assert.Equal("orders", request.BucketName);
        Assert.Equal("processed/A-100.json", request.Key);
        Assert.Equal("application/json", request.ContentType);
        Assert.Equal("{\"ok\":true}", request.ContentBody);
        Assert.Equal("north", request.Metadata["tenant"]);
        Assert.Equal("order", request.TagSet.Single(tag => tag.Key == "kind").Value);
        Assert.Equal("pipelinez", request.TagSet.Single(tag => tag.Key == "source").Value);
    }

    [Fact]
    public void PutObject_Rejects_Multiple_Content_Sources()
    {
        var putObject = new AmazonS3PutObject
        {
            Key = "one.txt",
            TextContent = "one",
            BinaryContent = Encoding.UTF8.GetBytes("two")
        };

        var exception = Assert.Throws<InvalidOperationException>(() => putObject.Validate());
        Assert.Contains("exactly one content source", exception.Message);
    }

    private static AmazonS3DestinationOptions CreateDestinationOptions()
    {
        return new AmazonS3DestinationOptions
        {
            BucketName = "orders",
            Write = new AmazonS3ObjectWriteOptions
            {
                KeyPrefix = "processed/",
                Tags = new Dictionary<string, string> { ["source"] = "pipelinez" }
            }
        };
    }
}
