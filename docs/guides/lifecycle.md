# Lifecycle

Audience: application developers working with pipeline startup, publishing, and shutdown behavior.

## What This Covers

- `Build()`
- `StartPipelineAsync()`
- `PublishAsync(...)`
- `CompleteAsync()`
- `Completion`

## Basic Lifecycle

```csharp
var pipeline = Pipeline<MyRecord>.New("orders")
    .WithInMemorySource(new object())
    .WithInMemoryDestination("config")
    .Build();

await pipeline.StartPipelineAsync();
await pipeline.PublishAsync(new MyRecord());
await pipeline.CompleteAsync();
await pipeline.Completion;
```

## Lifecycle Phases

1. Build the pipeline.
2. Start the pipeline.
3. Publish records or let the configured source run.
4. Complete the pipeline when no more records should enter.
5. Await `Completion` for full shutdown.

## Important Behaviors

- `StartPipelineAsync()` is async and should be awaited.
- `PublishAsync(...)` before start throws.
- `CompleteAsync()` before start throws.
- starting twice throws.
- `Completion` represents the full pipeline run, not just one internal block.
- `CompleteAsync()` waits for downstream work to finish rather than only stopping ingress.
- when the pipeline faults, `OnPipelineFaulted` is raised before `Completion` faults.

## Manual Publish With Flow Control

```csharp
var result = await pipeline.PublishAsync(
    new MyRecord(),
    new PipelinePublishOptions
    {
        Timeout = TimeSpan.FromSeconds(2)
    });

if (!result.Accepted)
{
    Console.WriteLine(result.Reason);
}
```

## Related Docs

- [In-Memory Pipeline](../getting-started/in-memory.md)
- [Flow Control](flow-control.md)
- [Error Handling](error-handling.md)
