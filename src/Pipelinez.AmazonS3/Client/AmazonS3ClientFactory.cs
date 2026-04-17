using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;
using Pipelinez.AmazonS3.Configuration;

namespace Pipelinez.AmazonS3.Client;

internal sealed class AmazonS3ClientFactory : IAmazonS3ClientFactory
{
    public static AmazonS3ClientFactory Instance { get; } = new();

    public IAmazonS3Client CreateClient(AmazonS3ConnectionOptions options)
    {
        var validatedOptions = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        var config = BuildConfig(validatedOptions);
        var credentials = ResolveCredentials(validatedOptions);
        var client = credentials is null
            ? new AmazonS3Client(config)
            : new AmazonS3Client(credentials, config);

        return new AmazonS3ClientAdapter(client);
    }

    private static AmazonS3Config BuildConfig(AmazonS3ConnectionOptions options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            if (!string.IsNullOrWhiteSpace(options.Region))
            {
                config.AuthenticationRegion = options.Region;
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        if (options.RequestChecksumCalculation is not null)
        {
            config.RequestChecksumCalculation = options.RequestChecksumCalculation.Value;
        }

        options.ConfigureClient?.Invoke(config);
        return config;
    }

    private static AWSCredentials? ResolveCredentials(AmazonS3ConnectionOptions options)
    {
        if (options.Credentials is not null)
        {
            return options.Credentials;
        }

        if (string.IsNullOrWhiteSpace(options.ProfileName))
        {
            return null;
        }

        var chain = new CredentialProfileStoreChain();
        if (!chain.TryGetAWSCredentials(options.ProfileName, out var credentials))
        {
            throw new InvalidOperationException($"AWS profile '{options.ProfileName}' could not be resolved.");
        }

        return credentials;
    }

    private sealed class AmazonS3ClientAdapter(IAmazonS3 client) : IAmazonS3Client
    {
        public Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken)
        {
            return client.ListObjectsV2Async(request, cancellationToken);
        }

        public Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken)
        {
            return client.GetObjectAsync(request, cancellationToken);
        }

        public Task<GetObjectTaggingResponse> GetObjectTaggingAsync(GetObjectTaggingRequest request, CancellationToken cancellationToken)
        {
            return client.GetObjectTaggingAsync(request, cancellationToken);
        }

        public Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken)
        {
            return client.PutObjectAsync(request, cancellationToken);
        }

        public Task<PutObjectTaggingResponse> PutObjectTaggingAsync(PutObjectTaggingRequest request, CancellationToken cancellationToken)
        {
            return client.PutObjectTaggingAsync(request, cancellationToken);
        }

        public Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request, CancellationToken cancellationToken)
        {
            return client.CopyObjectAsync(request, cancellationToken);
        }

        public Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken)
        {
            return client.DeleteObjectAsync(request, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
