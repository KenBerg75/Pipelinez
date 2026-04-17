# Amazon S3

Audience: application developers and operators using the Amazon S3 transport extension.

## What This Covers

- object enumeration sources
- object destinations
- Pipelinez dead-letter artifact publishing
- source object settlement
- S3-compatible endpoint configuration
- local Docker/Testcontainers usage

## Current Transport Status

Amazon S3 support lives in:

- `src/Pipelinez.AmazonS3`

The transport package is:

- `Pipelinez.AmazonS3`

## Basic Configuration

```csharp
using Pipelinez.AmazonS3.Configuration;

var connection = new AmazonS3ConnectionOptions
{
    Region = "us-east-1"
};

var sourceOptions = new AmazonS3SourceOptions
{
    Connection = connection,
    Bucket = new AmazonS3BucketOptions
    {
        BucketName = "orders",
        Prefix = "incoming/"
    }
};

var destinationOptions = new AmazonS3DestinationOptions
{
    Connection = connection,
    BucketName = "orders",
    Write = new AmazonS3ObjectWriteOptions
    {
        KeyPrefix = "processed/"
    }
};
```

For LocalStack, MinIO, or another S3-compatible endpoint, set `ServiceUrl`, `ForcePathStyle`, and `Region`. If the endpoint does not support AWS SDK flexible request checksums, set `RequestChecksumCalculation` to `RequestChecksumCalculation.WHEN_REQUIRED`.

## Source Example

```csharp
using System.Text;
using Pipelinez.AmazonS3;
using Pipelinez.AmazonS3.Source;
using Pipelinez.Core;

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithAmazonS3Source(
        sourceOptions,
        async (context, cancellationToken) =>
        {
            using var reader = new StreamReader(context.Content, Encoding.UTF8, leaveOpen: true);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            return new OrderRecord { Id = context.Key, Payload = payload };
        })
    .WithInMemoryDestination("memory")
    .Build();
```

The `AmazonS3ObjectContext.Content` stream is valid during mapper execution. Copy anything needed beyond the mapper into the returned record.

## Destination Example

```csharp
using System.Text.Json;
using Pipelinez.AmazonS3;
using Pipelinez.AmazonS3.Destination;

var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithAmazonS3Destination(
        destinationOptions,
        record => AmazonS3PutObject.FromText(
            $"{record.Id}.json",
            JsonSerializer.Serialize(record),
            "application/json"))
    .Build();
```

Destinations write one mapped object per successful record. The mapper controls object keys, content, content type, metadata, and tags.

## Dead-Letter Destination

Pipelinez dead-lettering writes a JSON artifact by default.

```csharp
var pipeline = Pipeline<OrderRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithInMemoryDestination("memory")
    .WithAmazonS3DeadLetterDestination(new AmazonS3DeadLetterOptions
    {
        Connection = connection,
        BucketName = "orders",
        KeyPrefix = "dead-letter/"
    })
    .Build();
```

Use the mapper overload when the application needs a custom dead-letter object shape.

## Source Settlement

S3 has no native acknowledgement model, so Pipelinez applies optional object settlement after the pipeline reaches a terminal outcome.

Supported actions:

- `Leave`
- `Delete`
- `Tag`
- `Copy`
- `Move`

```csharp
var sourceOptions = new AmazonS3SourceOptions
{
    Connection = connection,
    Bucket = new AmazonS3BucketOptions
    {
        BucketName = "orders",
        Prefix = "incoming/"
    },
    Settlement = new AmazonS3ObjectSettlementOptions
    {
        OnSuccess = AmazonS3ObjectSettlementAction.Move,
        SuccessPrefix = "archive/processed/",
        OnDeadLetter = AmazonS3ObjectSettlementAction.Move,
        DeadLetterPrefix = "archive/failed/",
        OnSkipped = AmazonS3ObjectSettlementAction.Tag,
        SkippedTags = new Dictionary<string, string>
        {
            ["pipelinez-status"] = "skipped"
        }
    }
};
```

Copy and move settlement preserve the object key relative to the configured source prefix. An object at `incoming/2026/04/order.json` moved to `archive/processed/` becomes `archive/processed/2026/04/order.json`.

## Metadata

The source stamps metadata keys such as:

- `pipelinez.amazon-s3.bucket-name`
- `pipelinez.amazon-s3.object-key`
- `pipelinez.amazon-s3.version-id`
- `pipelinez.amazon-s3.etag`
- `pipelinez.amazon-s3.size-bytes`
- `pipelinez.amazon-s3.last-modified`
- `pipelinez.amazon-s3.content-type`

Use `AmazonS3MetadataExtensions` on the container metadata when tests or custom components need strongly named accessors.

## Event Notifications

This transport enumerates and reads objects directly from S3. It does not consume S3 event notifications. Pair S3 notifications with an SQS transport when event-driven object ingestion is needed.

## Related Docs

- [Dead-Lettering](../guides/dead-lettering.md)
- [Flow Control](../guides/flow-control.md)
- [Lifecycle](../guides/lifecycle.md)
