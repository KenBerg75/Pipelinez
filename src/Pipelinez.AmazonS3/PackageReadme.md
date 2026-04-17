# Pipelinez.AmazonS3

Amazon S3 object source, destination, and dead-letter extensions for Pipelinez.

Use `Pipelinez.AmazonS3` when a Pipelinez pipeline needs to enumerate objects from S3, write processed records as S3 objects, or persist dead-letter records as reviewable S3 artifacts.

## What It Adds

- `WithAmazonS3Source(...)`
- `WithAmazonS3Destination(...)`
- `WithAmazonS3DeadLetterDestination(...)`
- object source metadata for bucket, key, version, ETag, size, content type, and last modified time
- configurable source settlement through leave, delete, tag, copy, or move behavior
- one-object-per-record destination and dead-letter writes

## Install

```bash
dotnet add package Pipelinez.AmazonS3
```

`Pipelinez.AmazonS3` depends on `Pipelinez`, so you do not need to add both explicitly unless you prefer to do so.

## Minimal Destination

```csharp
using System.Text;
using System.Text.Json;
using Pipelinez.AmazonS3;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.Core;
using Pipelinez.Core.Record;

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithAmazonS3Destination(
        new AmazonS3DestinationOptions
        {
            BucketName = "processed-orders"
        },
        record => AmazonS3PutObject.FromBytes(
            $"orders/{record.Id}.json",
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record)),
            "application/json"))
    .Build();

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }
}
```

## Scope

- Enumerate and read S3 objects as pipeline records.
- Write successful pipeline records to S3.
- Write dead-letter records to S3.
- Keep buckets, prefixes, keys, lifecycle policies, encryption, and IAM permissions owned by the application.

This package does not consume S3 event notifications directly. Pair S3 notifications with the SQS transport when event-driven ingestion is needed.

## Related Links

- NuGet: https://www.nuget.org/packages/Pipelinez.AmazonS3
- Documentation: https://github.com/KenBerg75/Pipelinez/tree/main/documentation
- Amazon S3 docs: https://github.com/KenBerg75/Pipelinez/blob/main/documentation/transports/amazon-s3.md
