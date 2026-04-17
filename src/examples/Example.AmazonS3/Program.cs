using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Pipelinez.AmazonS3;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.AmazonS3.Source;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Testcontainers.LocalStack;

await using var storage = await AmazonS3ExampleStorage.StartAsync();
await storage.CreateBucketAsync("pipelinez-orders");
await storage.PutObjectAsync("pipelinez-orders", "incoming/A-100.txt", "A-100|ok");
await storage.PutObjectAsync("pipelinez-orders", "incoming/A-200.txt", "A-200|fail");
await storage.PutObjectAsync("pipelinez-orders", "incoming/A-300.txt", "A-300|ok");

var pipeline = Pipeline<OrderRecord>.New("amazon-s3-orders")
    .WithAmazonS3Source(
        new AmazonS3SourceOptions
        {
            Connection = storage.Connection,
            Bucket = new AmazonS3BucketOptions
            {
                BucketName = "pipelinez-orders",
                Prefix = "incoming/"
            },
            Settlement = new AmazonS3ObjectSettlementOptions
            {
                OnSuccess = AmazonS3ObjectSettlementAction.Move,
                SuccessPrefix = "archive/processed/",
                OnDeadLetter = AmazonS3ObjectSettlementAction.Move,
                DeadLetterPrefix = "archive/failed/"
            }
        },
        MapOrderAsync)
    .AddSegment(new ValidateOrderSegment(), new object())
    .WithAmazonS3Destination(
        new AmazonS3DestinationOptions
        {
            Connection = storage.Connection,
            BucketName = "pipelinez-orders",
            Write = new AmazonS3ObjectWriteOptions { KeyPrefix = "processed/" }
        },
        record => AmazonS3PutObject.FromText(
            $"{record.Id}.json",
            $$"""{"id":"{{record.Id}}","payload":"{{record.Payload}}"}""",
            "application/json"))
    .WithAmazonS3DeadLetterDestination(
        new AmazonS3DeadLetterOptions
        {
            Connection = storage.Connection,
            BucketName = "pipelinez-orders",
            KeyPrefix = "dead-letter/"
        })
    .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
    .Build();

await pipeline.StartPipelineAsync();
await WaitForObjectsAsync(storage);
await pipeline.CompleteAsync();

Console.WriteLine("Processed objects:");
foreach (var key in await storage.ListKeysAsync("pipelinez-orders", "processed/"))
{
    Console.WriteLine($"  {key}");
}

Console.WriteLine("Dead-letter objects:");
foreach (var key in await storage.ListKeysAsync("pipelinez-orders", "dead-letter/"))
{
    Console.WriteLine($"  {key}");
}

static async Task<OrderRecord> MapOrderAsync(
    AmazonS3ObjectContext context,
    CancellationToken cancellationToken)
{
    using var reader = new StreamReader(context.Content, Encoding.UTF8, leaveOpen: true);
    var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    var parts = content.Split('|', 2);
    return new OrderRecord { Id = parts[0], Payload = parts[1] };
}

static async Task WaitForObjectsAsync(AmazonS3ExampleStorage storage)
{
    var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
    while (DateTimeOffset.UtcNow < deadline)
    {
        var processed = await storage.ListKeysAsync("pipelinez-orders", "processed/").ConfigureAwait(false);
        var deadLetters = await storage.ListKeysAsync("pipelinez-orders", "dead-letter/").ConfigureAwait(false);
        if (processed.Count == 2 && deadLetters.Count == 1)
        {
            return;
        }

        await Task.Delay(100).ConfigureAwait(false);
    }

    throw new TimeoutException("Timed out waiting for the Amazon S3 example pipeline to finish.");
}

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }

    public required string Payload { get; init; }
}

public sealed class ValidateOrderSegment : PipelineSegment<OrderRecord>
{
    public override Task<OrderRecord> ExecuteAsync(OrderRecord arg)
    {
        if (arg.Payload == "fail")
        {
            throw new InvalidOperationException($"Order {arg.Id} failed validation.");
        }

        return Task.FromResult(arg);
    }
}

internal sealed class AmazonS3ExampleStorage : IAsyncDisposable
{
#pragma warning disable CS0618
    private readonly LocalStackContainer? _container;
#pragma warning restore CS0618
    private readonly AmazonS3Client _client;

    private AmazonS3ExampleStorage(
        AmazonS3ConnectionOptions connection,
        AmazonS3Client client,
        LocalStackContainer? container)
    {
        Connection = connection;
        _client = client;
        _container = container;
    }

    public AmazonS3ConnectionOptions Connection { get; }

    public static async Task<AmazonS3ExampleStorage> StartAsync()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("PIPELINEZ_EXAMPLE_S3_SERVICE_URL");
        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            var connection = CreateConnection(serviceUrl);
            return new AmazonS3ExampleStorage(connection, CreateClient(connection), container: null);
        }

#pragma warning disable CS0618
        var container = new LocalStackBuilder().Build();
#pragma warning restore CS0618
        await container.StartAsync().ConfigureAwait(false);

        var localConnection = CreateConnection(container.GetConnectionString());
        return new AmazonS3ExampleStorage(localConnection, CreateClient(localConnection), container);
    }

    public async Task CreateBucketAsync(string bucketName)
    {
        await _client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }).ConfigureAwait(false);
    }

    public async Task PutObjectAsync(string bucketName, string key, string content)
    {
        await _client
            .PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                ContentBody = content,
                ContentType = "text/plain"
            })
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string bucketName, string prefix)
    {
        var response = await _client
            .ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = prefix
            })
            .ConfigureAwait(false);

        return (response.S3Objects ?? [])
            .Select(obj => obj.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static AmazonS3ConnectionOptions CreateConnection(string serviceUrl)
    {
        return new AmazonS3ConnectionOptions
        {
            ServiceUrl = serviceUrl,
            ForcePathStyle = true,
            Region = "us-east-1",
            Credentials = new BasicAWSCredentials("test", "test"),
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
        };
    }

    private static AmazonS3Client CreateClient(AmazonS3ConnectionOptions connection)
    {
        return new AmazonS3Client(
            connection.Credentials,
            new AmazonS3Config
            {
                ServiceURL = connection.ServiceUrl,
                ForcePathStyle = connection.ForcePathStyle,
                AuthenticationRegion = connection.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
            });
    }
}
