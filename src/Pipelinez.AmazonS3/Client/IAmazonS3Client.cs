using Amazon.S3.Model;

namespace Pipelinez.AmazonS3.Client;

internal interface IAmazonS3Client : IAsyncDisposable
{
    Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken);

    Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken);

    Task<GetObjectTaggingResponse> GetObjectTaggingAsync(GetObjectTaggingRequest request, CancellationToken cancellationToken);

    Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken);

    Task<PutObjectTaggingResponse> PutObjectTaggingAsync(PutObjectTaggingRequest request, CancellationToken cancellationToken);

    Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request, CancellationToken cancellationToken);

    Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken);
}
