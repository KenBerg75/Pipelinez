# Retry

Audience: application developers handling transient failures in segments or destinations.

## What This Covers

- pipeline-wide retry options
- component overrides
- retry events
- retry exhaustion behavior

## Pipeline-Wide Retry

```csharp
using Pipelinez.Core.Retry;

var pipeline = Pipeline<MyRecord>.New("orders")
    .UseRetryOptions(new PipelineRetryOptions<MyRecord>
    {
        DefaultSegmentPolicy = PipelineRetryPolicy<MyRecord>
            .ExponentialBackoff(
                maxAttempts: 5,
                initialDelay: TimeSpan.FromMilliseconds(100),
                maxDelay: TimeSpan.FromSeconds(3),
                useJitter: true)
            .Handle<TimeoutException>(),
        DestinationPolicy = PipelineRetryPolicy<MyRecord>
            .FixedDelay(maxAttempts: 3, delay: TimeSpan.FromSeconds(1))
    })
    .WithInMemorySource(new object())
    .AddSegment(new MySegment(), new object())
    .WithInMemoryDestination("config")
    .Build();
```

## Per-Component Override

```csharp
var retryPolicy = PipelineRetryPolicy<MyRecord>
    .FixedDelay(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(250))
    .Handle<TimeoutException>();

var pipeline = Pipeline<MyRecord>.New("orders")
    .WithInMemorySource(new object())
    .AddSegment(new MySegment(), new object(), retryPolicy)
    .WithInMemoryDestination("config")
    .Build();
```

## Retry Events

```csharp
pipeline.OnPipelineRecordRetrying += (_, args) =>
{
    Console.WriteLine(
        $"{args.ComponentName} retry {args.AttemptNumber}/{args.MaxAttempts} after {args.Delay}.");
};
```

## Important Behaviors

- retries happen before terminal error handling
- successful recovery does not leave the record in a faulted state
- exhausted retries flow into the normal error-handler path
- retry history is stored on `PipelineContainer<T>.RetryHistory`

## Related Docs

- [Error Handling](error-handling.md)
- [Dead-Lettering](dead-lettering.md)
- [Operational Tooling](operational-tooling.md)
