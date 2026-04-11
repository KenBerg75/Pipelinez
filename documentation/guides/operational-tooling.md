# Operational Tooling

Audience: operators and application developers integrating Pipelinez into hosted services.

## What This Covers

- health snapshots
- operational snapshots
- health-check integration
- runtime metrics
- correlation IDs

## Enable Operational Features

```csharp
using Pipelinez.Core.Operational;

var pipeline = Pipeline<MyRecord>.New("orders")
    .UseOperationalOptions(new PipelineOperationalOptions
    {
        EnableHealthChecks = true,
        EnableMetrics = true,
        EnableCorrelationIds = true,
        DeadLetterDegradedThreshold = 1,
        PublishRejectionDegradedThreshold = 1
    })
    .WithInMemorySource(new object())
    .WithInMemoryDestination("config")
    .Build();
```

## Read Health And Operational Snapshots

```csharp
var health = pipeline.GetHealthStatus();
var snapshot = pipeline.GetOperationalSnapshot();

Console.WriteLine(health.State);
Console.WriteLine(snapshot.Performance.RecordsPerSecond);
Console.WriteLine(snapshot.LastRecordCompletedAtUtc);
```

## ASP.NET Core Health Checks

```csharp
using Pipelinez.Core.Operational;

builder.Services.AddSingleton(pipeline);
builder.Services.AddHealthChecks()
    .AddCheck("orders-pipeline", new PipelineHealthCheck<MyRecord>(pipeline));
```

## Metrics

Pipelinez emits runtime metrics through the `Pipelinez.Runtime` meter.

Example OpenTelemetry registration:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Pipelinez.Runtime");
    });
```

## Correlation IDs

When enabled, Pipelinez stamps a correlation ID into metadata using:

- `pipelinez.correlation.id`

This is surfaced through event diagnostic payloads so logs and incidents can be traced across retry, fault, and dead-letter behavior.

## Related Docs

- [Troubleshooting](../operations/troubleshooting.md)
- [Distributed Execution](distributed-execution.md)
- [Architecture: Testing](../architecture/testing.md)
