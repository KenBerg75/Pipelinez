using Pipelinez.AmazonS3.Configuration;

namespace Pipelinez.AmazonS3.Client;

internal interface IAmazonS3ClientFactory
{
    IAmazonS3Client CreateClient(AmazonS3ConnectionOptions options);
}
