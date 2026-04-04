using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Operational;
using Pipelinez.Core.Retry;
using Pipelinez.Core.Source;
using Pipelinez.Tests.Core.DeadLetterTests.Models;
using Pipelinez.Tests.Core.ErrorHandlingTests.Models;
using Pipelinez.Tests.Core.RetryTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.OperationalTests;

public sealed class PipelineOperationalToolingTests
{
    [Fact]
    public async Task Pipeline_Health_Status_Transitions_Across_Runtime_Lifecycle()
    {
        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_Health_Status_Transitions_Across_Runtime_Lifecycle))
            .WithInMemorySource(new object())
            .WithInMemoryDestination("config")
            .Build();

        Assert.Equal(PipelineHealthState.Starting, pipeline.GetHealthStatus().State);

        await pipeline.StartPipelineAsync();

        Assert.Equal(PipelineHealthState.Healthy, pipeline.GetHealthStatus().State);

        await pipeline.PublishAsync(new TestPipelineRecord { Data = "ok" });
        await pipeline.CompleteAsync();
        await pipeline.Completion;

        Assert.Equal(PipelineHealthState.Completed, pipeline.GetHealthStatus().State);
    }

    [Fact]
    public async Task Pipeline_Health_Status_Becomes_Degraded_When_DeadLetter_Threshold_Is_Exceeded()
    {
        var deadLetters = new InMemoryDeadLetterDestination<TestPipelineRecord>();
        var destination = new CollectingDestination();

        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_Health_Status_Becomes_Degraded_When_DeadLetter_Threshold_Is_Exceeded))
            .UseOperationalOptions(new PipelineOperationalOptions
            {
                DeadLetterDegradedThreshold = 1,
                RetryExhaustionDegradedThreshold = long.MaxValue,
                PublishRejectionDegradedThreshold = long.MaxValue,
                DrainingPartitionDegradedThreshold = int.MaxValue,
                SaturationDegradedThreshold = 1d
            })
            .WithInMemorySource(new object())
            .AddSegment(new ConditionalFaultingSegment(), new object())
            .WithDestination(destination)
            .WithDeadLetterDestination(deadLetters)
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });

        await WaitForConditionAsync(() => deadLetters.Records.Count == 1);

        var health = pipeline.GetHealthStatus();
        Assert.Equal(PipelineHealthState.Degraded, health.State);
        Assert.Contains(health.Reasons, reason => reason.Contains("Dead-letter", StringComparison.OrdinalIgnoreCase));

        await pipeline.CompleteAsync();
        await pipeline.Completion;
    }

    [Fact]
    public async Task Pipeline_Operational_Snapshot_Includes_Last_Completion_And_Last_DeadLetter_Timestamps()
    {
        var deadLetters = new InMemoryDeadLetterDestination<TestPipelineRecord>();
        var destination = new CollectingDestination();

        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_Operational_Snapshot_Includes_Last_Completion_And_Last_DeadLetter_Timestamps))
            .WithInMemorySource(new object())
            .AddSegment(new ConditionalFaultingSegment(), new object())
            .WithDestination(destination)
            .WithDeadLetterDestination(deadLetters)
            .WithErrorHandler(context =>
                context.Record.Data == ConditionalFaultingSegment.FaultingValue
                    ? PipelineErrorAction.DeadLetter
                    : PipelineErrorAction.StopPipeline)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "good" });

        await WaitForConditionAsync(() => deadLetters.Records.Count == 1 && destination.Records.Count == 1);

        var snapshot = pipeline.GetOperationalSnapshot();
        Assert.NotNull(snapshot.LastDeadLetteredAtUtc);
        Assert.NotNull(snapshot.LastRecordCompletedAtUtc);
        Assert.Equal(1, snapshot.Performance.TotalDeadLetteredCount);
        Assert.Equal(1, snapshot.Performance.TotalRecordsCompleted);

        await pipeline.CompleteAsync();
        await pipeline.Completion;
    }

    [Fact]
    public async Task Pipeline_Correlation_Id_Is_Generated_And_Preserved()
    {
        var source = new InMemoryPipelineSource<TestPipelineRecord>();
        var completed = new List<string>();

        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_Correlation_Id_Is_Generated_And_Preserved))
            .WithSource(source)
            .WithInMemoryDestination("config")
            .Build();

        pipeline.OnPipelineRecordCompleted += (_, args) =>
        {
            completed.Add(args.Diagnostic!.CorrelationId);
        };

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "auto" });

        var preservedMetadata = new Pipelinez.Core.Record.Metadata.MetadataCollection();
        preservedMetadata.Set(PipelineOperationalMetadataKeys.CorrelationId, "custom-correlation-id");
        await source.PublishAsync(
            new TestPipelineRecord { Data = "preserved" },
            preservedMetadata);

        await pipeline.CompleteAsync();
        await pipeline.Completion;

        Assert.Equal(2, completed.Count);
        Assert.False(string.IsNullOrWhiteSpace(completed[0]));
        Assert.Equal("custom-correlation-id", completed[1]);
    }

    [Fact]
    public async Task Pipeline_Health_Check_Maps_Unhealthy_Runtime()
    {
        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_Health_Check_Maps_Unhealthy_Runtime))
            .WithInMemorySource(new object())
            .AddSegment(new ConditionalFaultingSegment(), new object())
            .WithInMemoryDestination("config")
            .WithErrorHandler(_ => PipelineErrorAction.StopPipeline)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.Completion);

        var healthCheck = new PipelineHealthCheck<TestPipelineRecord>(pipeline);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Pipeline_Metrics_Are_Emitted_Through_Runtime_Meter()
    {
        var pipelineName = nameof(Pipeline_Metrics_Are_Emitted_Through_Runtime_Meter);
        var measurements = new Dictionary<string, long>(StringComparer.Ordinal);

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == PipelineMetricsEmitter.MeterName)
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (!HasPipelineTag(tags, pipelineName))
            {
                return;
            }

            measurements[instrument.Name] = measurements.GetValueOrDefault(instrument.Name) + measurement;
        });
        listener.Start();

        var pipeline = Pipeline<TestPipelineRecord>.New(pipelineName)
            .UseRetryOptions(new PipelineRetryOptions<TestPipelineRecord>
            {
                DefaultSegmentPolicy = PipelineRetryPolicy<TestPipelineRecord>
                    .FixedDelay(3, TimeSpan.Zero)
                    .Handle<RetryTestException>()
            })
            .WithInMemorySource(new object())
            .AddSegment(new FlakySegment(failuresBeforeSuccess: 1), new object())
            .WithInMemoryDestination("config")
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "good" });
        await pipeline.CompleteAsync();
        await pipeline.Completion;

        Assert.True(measurements["pipelinez.records.published"] >= 1);
        Assert.True(measurements["pipelinez.records.completed"] >= 1);
        Assert.True(measurements["pipelinez.retry.attempts"] >= 1);
        Assert.True(measurements["pipelinez.retry.recoveries"] >= 1);
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for the expected operational test condition.");
    }

    private static bool HasPipelineTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string pipelineName)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "pipeline.name" && string.Equals(tag.Value?.ToString(), pipelineName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
