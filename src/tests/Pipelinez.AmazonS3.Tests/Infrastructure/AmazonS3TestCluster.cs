using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Pipelinez.AmazonS3.Configuration;
using Testcontainers.LocalStack;
using Xunit;

namespace Pipelinez.AmazonS3.Tests.Infrastructure;

public sealed class AmazonS3TestCluster : IAsyncLifetime, IAsyncDisposable
{
#pragma warning disable CS0618
    private readonly LocalStackContainer _container = new LocalStackBuilder().Build();
#pragma warning restore CS0618
    private AmazonS3Client? _client;

    public TimeSpan ObservationTimeout { get; } = TimeSpan.FromSeconds(20);

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        _client = new AmazonS3Client(
            new BasicAWSCredentials("test", "test"),
            CreateClientConfig());
    }

    public Task DisposeAsync()
    {
        return DisposeAsyncCore().AsTask();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
    }

    public AmazonS3ConnectionOptions CreateConnectionOptions()
    {
        return new AmazonS3ConnectionOptions
        {
            ServiceUrl = _container.GetConnectionString(),
            ForcePathStyle = true,
            Region = "us-east-1",
            Credentials = new BasicAWSCredentials("test", "test"),
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
        };
    }

    public async Task CreateBucketAsync(string bucketName)
    {
        await Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }).ConfigureAwait(false);
    }

    public async Task PutObjectAsync(
        string bucketName,
        string key,
        string content,
        string contentType = "text/plain")
    {
        await Client
            .PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                ContentBody = content,
                ContentType = contentType
            })
            .ConfigureAwait(false);
    }

    public async Task<string> GetObjectContentAsync(string bucketName, string key)
    {
        using var response = await Client
            .GetObjectAsync(bucketName, key)
            .ConfigureAwait(false);
        using var reader = new StreamReader(response.ResponseStream);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    public async Task<bool> ObjectExistsAsync(string bucketName, string key)
    {
        try
        {
            var response = await Client
                .GetObjectMetadataAsync(bucketName, key)
                .ConfigureAwait(false);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<string> WaitForObjectContentAsync(string bucketName, string key)
    {
        var deadline = DateTimeOffset.UtcNow + ObservationTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await ObjectExistsAsync(bucketName, key).ConfigureAwait(false))
            {
                return await GetObjectContentAsync(bucketName, key).ConfigureAwait(false);
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for S3 object '{key}' in bucket '{bucketName}'.");
    }

    public async Task<IReadOnlyList<S3Object>> ListObjectsAsync(string bucketName, string prefix)
    {
        var response = await Client
            .ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = prefix
            })
            .ConfigureAwait(false);
        return response.S3Objects ?? [];
    }

    private AmazonS3Config CreateClientConfig()
    {
        return new AmazonS3Config
        {
            ServiceURL = _container.GetConnectionString(),
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
        };
    }

    private async ValueTask DisposeAsyncCore()
    {
        _client?.Dispose();
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    private AmazonS3Client Client =>
        _client ?? throw new InvalidOperationException("LocalStack S3 client has not been initialized.");
}
