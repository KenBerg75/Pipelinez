# Distributed Execution

Audience: application developers running multiple workers against a distributed-capable source.

## What This Covers

- `PipelineExecutionMode.Distributed`
- `PipelineHostOptions`
- `GetRuntimeContext()`
- worker and partition events
- Kafka and Azure Service Bus distributed transport behavior

## Enable Distributed Mode

```csharp
using Pipelinez.Core.Distributed;

var pipeline = Pipeline<MyRecord>.New("orders")
    .UseHostOptions(new PipelineHostOptions
    {
        ExecutionMode = PipelineExecutionMode.Distributed,
        InstanceId = Environment.MachineName,
        WorkerId = $"orders-{Guid.NewGuid():N}"
    })
    .WithKafkaSource(...)
    .WithKafkaDestination(...)
    .Build();
```

## Inspect Runtime Context

```csharp
var runtime = pipeline.GetRuntimeContext();

Console.WriteLine(runtime.WorkerId);
Console.WriteLine(runtime.ExecutionMode);
Console.WriteLine(runtime.OwnedPartitions.Count);
```

## Observe Ownership Changes

```csharp
pipeline.OnPartitionsAssigned += (_, args) =>
{
    Console.WriteLine(
        $"Assigned {args.Partitions.Count} partitions to {args.RuntimeContext.WorkerId}");
};

pipeline.OnPartitionDraining += (_, args) =>
{
    Console.WriteLine($"Draining {args.Partition.LeaseId}");
};
```

## Important Behaviors

- distributed mode requires a source that supports distributed ownership
- Kafka reports explicit partition ownership and partition execution state
- Azure Service Bus reports a logical queue or subscription lease while Service Bus handles competing-consumer message distribution
- `GetStatus()` and `GetRuntimeContext()` both expose ownership and execution information
- partition drain and execution-state events are intended for observability, not direct transport control

## Related Docs

- [Kafka Transport](../transports/kafka.md)
- [Azure Service Bus Transport](../transports/azure-service-bus.md)
- [Operational Tooling](operational-tooling.md)
- [Architecture: Kafka Internals](../architecture/kafka.md)
- [Architecture: Azure Service Bus Internals](../architecture/azure-service-bus.md)
