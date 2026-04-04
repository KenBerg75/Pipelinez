# Dead-Lettering

Audience: application developers who want to preserve failed records for later inspection or replay.

## What This Covers

- `PipelineErrorAction.DeadLetter`
- dead-letter destinations
- `PipelineDeadLetterRecord<T>`
- dead-letter events

## In-Memory Dead-Lettering

```csharp
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;

var deadLetters = new InMemoryDeadLetterDestination<MyRecord>();

var pipeline = Pipeline<MyRecord>.New("orders")
    .WithInMemorySource(new object())
    .AddSegment(new MySegment(), new object())
    .WithInMemoryDestination("config")
    .WithDeadLetterDestination(deadLetters)
    .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
    .Build();
```

## What A Dead-Letter Record Contains

`PipelineDeadLetterRecord<T>` preserves:

- the original record
- `PipelineFaultState`
- metadata
- segment history
- retry history
- distribution context
- created and dead-lettered timestamps

## Dead-Letter Events

```csharp
pipeline.OnPipelineRecordDeadLettered += (_, args) =>
{
    Console.WriteLine(
        $"Dead-lettered {args.Record.Id} from {args.DeadLetterRecord.Fault.ComponentName}");
};

pipeline.OnPipelineDeadLetterWriteFailed += (_, args) =>
{
    Console.WriteLine(args.Exception.Message);
};
```

## Important Behaviors

- dead-lettering happens in the terminal fault-handling path
- a faulted record is treated as handled after a successful dead-letter write
- dead-lettered records do not raise the normal completion event
- if `DeadLetter` is chosen without a configured dead-letter destination, the pipeline faults

## Related Docs

- [Error Handling](error-handling.md)
- [Kafka Transport](../transports/kafka.md)
