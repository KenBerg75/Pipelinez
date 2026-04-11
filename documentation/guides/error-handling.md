# Error Handling

Audience: application developers deciding what should happen when a record faults.

## What This Covers

- default fault behavior
- `WithErrorHandler(...)`
- `PipelineErrorAction`
- how error handling interacts with retry and dead-lettering

## Default Behavior

If no custom error handler is configured, a faulted record causes the pipeline to stop.

## Configure An Error Handler

```csharp
using Pipelinez.Core.ErrorHandling;

var pipeline = Pipeline<MyRecord>.New("orders")
    .WithInMemorySource(new object())
    .AddSegment(new MySegment(), new object())
    .WithInMemoryDestination("config")
    .WithErrorHandler(context =>
    {
        if (context.RetryExhausted)
        {
            return PipelineErrorAction.DeadLetter;
        }

        return PipelineErrorAction.StopPipeline;
    })
    .Build();
```

## Available Actions

- `SkipRecord`
  stop processing the current faulted record and continue later records
- `DeadLetter`
  write the faulted record to the configured dead-letter destination
- `StopPipeline`
  fault and stop the runtime
- `Rethrow`
  fault the runtime and surface the original exception path

## What You Get In `PipelineErrorContext<T>`

- `Exception`
- `Record`
- `Container`
- `Fault`
- `ComponentName`
- `ComponentKind`
- `RetryHistory`
- `RetryAttemptCount`
- `RetryExhausted`
- `CancellationToken`

## Fault Event Example

```csharp
pipeline.OnPipelineRecordFaulted += (_, args) =>
{
    Console.WriteLine(
        $"Record faulted in {args.Fault.ComponentName}: {args.Fault.Exception.Message}");
};
```

When a fault stops the pipeline, `OnPipelineFaulted` is raised before `Completion` faults. If a fault-event subscriber throws, that subscriber exception is logged and the original pipeline fault remains the `Completion` exception.

## Related Docs

- [Retry](retry.md)
- [Dead-Lettering](dead-lettering.md)
- [Troubleshooting](../operations/troubleshooting.md)
