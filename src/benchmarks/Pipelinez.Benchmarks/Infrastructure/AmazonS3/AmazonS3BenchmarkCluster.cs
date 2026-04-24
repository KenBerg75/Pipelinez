using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Pipelinez.AmazonS3.Configuration;
using Testcontainers.LocalStack;

namespace Pipelinez.Benchmarks;

internal sealed class AmazonS3BenchmarkCluster : IAsyncDisposable
{
#pragma warning disable CS0618
    private readonly LocalStackContainer _container = new LocalStackBuilder().Build();
#pragma warning restore CS0618
    private AmazonS3Client? _client;

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        _client = new AmazonS3Client(
            new BasicAWSCredentials("test", "test"),
            CreateClientConfig());
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

    public async Task PutObjectAsync(string bucketName, string key, string content, string contentType = "text/plain")
    {
        await Client
            .PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    ContentBody = content,
                    ContentType = contentType
                })
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListObjectKeysAsync(string bucketName, string prefix)
    {
        var keys = new List<string>();
        string? continuationToken = null;

        do
        {
            var response = await Client
                .ListObjectsV2Async(
                    new ListObjectsV2Request
                    {
                        BucketName = bucketName,
                        Prefix = prefix,
                        ContinuationToken = continuationToken
                    })
                .ConfigureAwait(false);

            keys.AddRange(response.S3Objects.Select(obj => obj.Key));
            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return keys;
    }

    public Task WaitForObjectCountAsync(string bucketName, string prefix, int expectedCount, TimeSpan timeout)
    {
        return BenchmarkPolling.WaitUntilAsync(
            async () => (await ListObjectKeysAsync(bucketName, prefix).ConfigureAwait(false)).Count >= expectedCount,
            timeout,
            $"Timed out waiting for {expectedCount} objects in bucket '{bucketName}' with prefix '{prefix}'.");
    }

    public async Task DeleteBucketAsync(string bucketName)
    {
        var keys = await ListObjectKeysAsync(bucketName, string.Empty).ConfigureAwait(false);
        if (keys.Count > 0)
        {
            await Client
                .DeleteObjectsAsync(
                    new DeleteObjectsRequest
                    {
                        BucketName = bucketName,
                        Objects = keys.Select(key => new KeyVersion { Key = key }).ToList()
                    })
                .ConfigureAwait(false);
        }

        try
        {
            await Client.DeleteBucketAsync(bucketName).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    private AmazonS3Client Client =>
        _client ?? throw new InvalidOperationException("The Amazon S3 benchmark cluster has not been initialized.");

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
}
