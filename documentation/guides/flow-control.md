# Flow Control

Audience: application developers publishing records manually into a busy pipeline.

## What This Covers

- pipeline saturation behavior
- `PipelineFlowControlOptions`
- `PipelinePublishOptions`
- `PipelinePublishResult`

## Configure Flow Control

```csharp
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Performance;

var pipeline = Pipeline<MyRecord>.New("orders")
    .UsePerformanceOptions(new PipelinePerformanceOptions
    {
        SourceExecution = new PipelineExecutionOptions { BoundedCapacity = 100 },
        DestinationExecution = new PipelineExecutionOptions { BoundedCapacity = 100 }
    })
    .UseFlowControlOptions(new PipelineFlowControlOptions
    {
        OverflowPolicy = PipelineOverflowPolicy.Wait,
        PublishTimeout = TimeSpan.FromSeconds(5),
        SaturationWarningThreshold = 0.8
    })
    .WithInMemorySource(new object())
    .WithInMemoryDestination("config")
    .Build();
```

## Per-Publish Override

```csharp
var result = await pipeline.PublishAsync(
    new MyRecord(),
    new PipelinePublishOptions
    {
        Timeout = TimeSpan.FromSeconds(2),
        OverflowPolicyOverride = PipelineOverflowPolicy.Reject
    });

if (!result.Accepted)
{
    Console.WriteLine(result.Reason);
}
```

## Overflow Policies

- `Wait`
  wait for capacity
- `Reject`
  reject the publish immediately when saturated
- `Cancel`
  allow a cancellation token to end the wait

## Observability

Flow-control signals are exposed through:

- `GetStatus().FlowControlStatus`
- `OnSaturationChanged`
- `OnPublishRejected`
- publish wait and rejection counters in `GetPerformanceSnapshot()`

## Related Docs

- [Lifecycle](lifecycle.md)
- [Performance](performance.md)
- [Operational Tooling](operational-tooling.md)
