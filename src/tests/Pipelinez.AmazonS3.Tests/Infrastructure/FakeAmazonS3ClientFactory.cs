using Amazon.S3.Model;
using Pipelinez.AmazonS3.Client;
using Pipelinez.AmazonS3.Configuration;

namespace Pipelinez.AmazonS3.Tests.Infrastructure;

internal sealed class FakeAmazonS3ClientFactory : IAmazonS3ClientFactory
{
    public FakeAmazonS3Client Client { get; } = new();

    public IAmazonS3Client CreateClient(AmazonS3ConnectionOptions options)
    {
        return Client;
    }
}

internal sealed class FakeAmazonS3Client : IAmazonS3Client
{
    private readonly List<PutObjectRequest> _putObjectRequests = new();
    private readonly List<PutObjectTaggingRequest> _taggingRequests = new();
    private readonly List<CopyObjectRequest> _copyObjectRequests = new();
    private readonly List<DeleteObjectRequest> _deleteObjectRequests = new();
    private readonly Queue<ListObjectsV2Response> _listResponses = new();
    private readonly Dictionary<string, GetObjectResponse> _objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Tag>> _tags = new(StringComparer.Ordinal);

    public IReadOnlyList<PutObjectRequest> PutObjectRequests => _putObjectRequests.ToArray();

    public IReadOnlyList<PutObjectTaggingRequest> TaggingRequests => _taggingRequests.ToArray();

    public IReadOnlyList<CopyObjectRequest> CopyObjectRequests => _copyObjectRequests.ToArray();

    public IReadOnlyList<DeleteObjectRequest> DeleteObjectRequests => _deleteObjectRequests.ToArray();

    public ListObjectsV2Request? LastListRequest { get; private set; }

    public Exception? PutObjectException { get; set; }

    public void EnqueueListResponse(ListObjectsV2Response response)
    {
        _listResponses.Enqueue(response);
    }

    public void AddObject(
        string key,
        string content,
        string contentType = "text/plain",
        string? versionId = null)
    {
        _objects[key] = new GetObjectResponse
        {
            BucketName = "orders",
            Key = key,
            VersionId = versionId,
            ResponseStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),
            Headers =
            {
                ContentType = contentType
            }
        };
    }

    public void AddTags(string key, params Tag[] tags)
    {
        _tags[key] = tags.ToList();
    }

    public Task<ListObjectsV2Response> ListObjectsV2Async(
        ListObjectsV2Request request,
        CancellationToken cancellationToken)
    {
        LastListRequest = request;
        return Task.FromResult(_listResponses.Count > 0 ? _listResponses.Dequeue() : new ListObjectsV2Response());
    }

    public Task<GetObjectResponse> GetObjectAsync(
        GetObjectRequest request,
        CancellationToken cancellationToken)
    {
        if (!_objects.TryGetValue(request.Key, out var response))
        {
            throw new InvalidOperationException($"No fake S3 object registered for key '{request.Key}'.");
        }

        response.ResponseStream.Position = 0;
        return Task.FromResult(response);
    }

    public Task<GetObjectTaggingResponse> GetObjectTaggingAsync(
        GetObjectTaggingRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new GetObjectTaggingResponse
        {
            Tagging = _tags.TryGetValue(request.Key, out var tags) ? tags : new List<Tag>()
        });
    }

    public Task<PutObjectResponse> PutObjectAsync(
        PutObjectRequest request,
        CancellationToken cancellationToken)
    {
        if (PutObjectException is not null)
        {
            throw PutObjectException;
        }

        _putObjectRequests.Add(request);
        return Task.FromResult(new PutObjectResponse());
    }

    public Task<PutObjectTaggingResponse> PutObjectTaggingAsync(
        PutObjectTaggingRequest request,
        CancellationToken cancellationToken)
    {
        _taggingRequests.Add(request);
        return Task.FromResult(new PutObjectTaggingResponse());
    }

    public Task<CopyObjectResponse> CopyObjectAsync(
        CopyObjectRequest request,
        CancellationToken cancellationToken)
    {
        _copyObjectRequests.Add(request);
        return Task.FromResult(new CopyObjectResponse());
    }

    public Task<DeleteObjectResponse> DeleteObjectAsync(
        DeleteObjectRequest request,
        CancellationToken cancellationToken)
    {
        _deleteObjectRequests.Add(request);
        return Task.FromResult(new DeleteObjectResponse());
    }

    public ValueTask DisposeAsync()
    {
        foreach (var response in _objects.Values)
        {
            response.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
