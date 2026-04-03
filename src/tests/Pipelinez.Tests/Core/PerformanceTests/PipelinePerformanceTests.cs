using Pipelinez.Core;
using Pipelinez.Core.Destination;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Source;
using Pipelinez.Tests.Core.ErrorHandlingTests.Models;
using Pipelinez.Tests.Core.PerformanceTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.PerformanceTests;

public class PipelinePerformanceTests
{
    [Fact]
    public void Pipeline_Build_Applies_Pipeline_Wide_Performance_Options_To_Components()
    {
        var source = new InMemoryPipelineSource<TestPipelineRecord>();
        var segment = new PassThroughPerformanceSegment();
        var destination = new InMemoryPipelineDestination<TestPipelineRecord>();

        var performanceOptions = new PipelinePerformanceOptions
        {
            SourceExecution = new PipelineExecutionOptions
            {
                BoundedCapacity = 111,
                DegreeOfParallelism = 1,
                EnsureOrdered = true
            },
            DefaultSegmentExecution = new PipelineExecutionOptions
            {
                BoundedCapacity = 222,
                DegreeOfParallelism = 3,
                EnsureOrdered = false
            },
            DestinationExecution = new PipelineExecutionOptions
            {
                BoundedCapacity = 333,
                DegreeOfParallelism = 1,
                EnsureOrdered = false
            }
        };

        var pipeline = Pipeline<TestPipelineRecord>.New("performance-defaults")
            .UsePerformanceOptions(performanceOptions)
            .WithSource(source)
            .AddSegment(segment, new object())
            .WithDestination(destination)
            .Build();

        Assert.NotNull(pipeline);
        AssertExecutionOptions(source.GetExecutionOptions(), boundedCapacity: 111, degreeOfParallelism: 1, ensureOrdered: true);
        AssertExecutionOptions(segment.GetExecutionOptions(), boundedCapacity: 222, degreeOfParallelism: 3, ensureOrdered: false);
        AssertExecutionOptions(destination.GetExecutionOptions(), boundedCapacity: 333, degreeOfParallelism: 1, ensureOrdered: false);
    }

    [Fact]
    public void Pipeline_Build_Component_Overrides_Take_Precedence_Over_Pipeline_Defaults()
    {
        var source = new InMemoryPipelineSource<TestPipelineRecord>();
        var segment = new PassThroughPerformanceSegment();
        var destination = new InMemoryPipelineDestination<TestPipelineRecord>();

        var pipeline = Pipeline<TestPipelineRecord>.New("performance-overrides")
            .UsePerformanceOptions(new PipelinePerformanceOptions
            {
                SourceExecution = new PipelineExecutionOptions { BoundedCapacity = 100, DegreeOfParallelism = 1, EnsureOrdered = true },
                DefaultSegmentExecution = new PipelineExecutionOptions { BoundedCapacity = 200, DegreeOfParallelism = 2, EnsureOrdered = true },
                DestinationExecution = new PipelineExecutionOptions { BoundedCapacity = 300, DegreeOfParallelism = 1, EnsureOrdered = true }
            })
            .WithSource(source, new PipelineExecutionOptions
            {
                BoundedCapacity = 11,
                DegreeOfParallelism = 1,
                EnsureOrdered = false
            })
            .AddSegment(segment, new object(), new PipelineExecutionOptions
            {
                BoundedCapacity = 22,
                DegreeOfParallelism = 8,
                EnsureOrdered = false
            })
            .WithDestination(destination, new PipelineExecutionOptions
            {
                BoundedCapacity = 33,
                DegreeOfParallelism = 1,
                EnsureOrdered = false
            })
            .Build();

        Assert.NotNull(pipeline);
        AssertExecutionOptions(source.GetExecutionOptions(), boundedCapacity: 11, degreeOfParallelism: 1, ensureOrdered: false);
        AssertExecutionOptions(segment.GetExecutionOptions(), boundedCapacity: 22, degreeOfParallelism: 8, ensureOrdered: false);
        AssertExecutionOptions(destination.GetExecutionOptions(), boundedCapacity: 33, degreeOfParallelism: 1, ensureOrdered: false);
    }

    [Fact]
    public async Task Pipeline_Performance_Snapshot_Tracks_Published_Completed_And_Faulted_Records()
    {
        var pipeline = Pipeline<TestPipelineRecord>.New("performance-snapshot")
            .UsePerformanceOptions(new PipelinePerformanceOptions())
            .WithInMemorySource(new object())
            .AddSegment(new ConditionalFaultingSegment(), new object())
            .WithInMemoryDestination("config")
            .WithErrorHandler(_ => PipelineErrorAction.SkipRecord)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await pipeline.PublishAsync(new TestPipelineRecord { Data = "good" }).ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue }).ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        var snapshot = pipeline.GetPerformanceSnapshot();

        Assert.Equal(2, snapshot.TotalRecordsPublished);
        Assert.Equal(1, snapshot.TotalRecordsCompleted);
        Assert.Equal(1, snapshot.TotalRecordsFaulted);
        Assert.True(snapshot.RecordsPerSecond > 0);
        Assert.True(snapshot.AverageEndToEndLatency >= TimeSpan.Zero);
        Assert.Contains(snapshot.Components, component => component.ComponentName.StartsWith("Source:", StringComparison.Ordinal));
        Assert.Contains(snapshot.Components, component => component.ComponentName.StartsWith("Segment[0]:", StringComparison.Ordinal));
        Assert.Contains(snapshot.Components, component => component.ComponentName.StartsWith("Destination:", StringComparison.Ordinal));

        var segmentSnapshot = snapshot.Components.Single(component => component.ComponentName.StartsWith("Segment[0]:", StringComparison.Ordinal));
        Assert.Equal(2, segmentSnapshot.ProcessedCount);
        Assert.Equal(1, segmentSnapshot.FaultedCount);

        var destinationSnapshot = snapshot.Components.Single(component => component.ComponentName.StartsWith("Destination:", StringComparison.Ordinal));
        Assert.Equal(1, destinationSnapshot.ProcessedCount);
        Assert.Equal(0, destinationSnapshot.FaultedCount);
    }

    [Fact]
    public async Task Pipeline_Performance_Options_Enable_Parallel_Segment_Execution()
    {
        var segment = new ParallelTrackingSegment();

        var pipeline = Pipeline<TestPipelineRecord>.New("performance-parallelism")
            .UsePerformanceOptions(new PipelinePerformanceOptions
            {
                DefaultSegmentExecution = new PipelineExecutionOptions
                {
                    BoundedCapacity = 64,
                    DegreeOfParallelism = 4,
                    EnsureOrdered = false
                }
            })
            .WithInMemorySource(new object())
            .AddSegment(segment, new object())
            .WithInMemoryDestination("config")
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        foreach (var value in Enumerable.Range(1, 8))
        {
            await pipeline.PublishAsync(new TestPipelineRecord { Data = $"record-{value}" }).ConfigureAwait(false);
        }

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.True(segment.MaxConcurrency > 1);

        var snapshot = pipeline.GetPerformanceSnapshot();
        var segmentSnapshot = snapshot.Components.Single(component => component.ComponentName.StartsWith("Segment[0]:", StringComparison.Ordinal));
        Assert.Equal(8, segmentSnapshot.ProcessedCount);
        Assert.True(segmentSnapshot.RecordsPerSecond > 0);
    }

    [Fact]
    public async Task Pipeline_Batching_Uses_Configured_Batch_Size_And_Flushes_On_Completion()
    {
        var destination = new CollectingBatchDestination();

        var pipeline = Pipeline<TestPipelineRecord>.New("performance-batching")
            .UsePerformanceOptions(new PipelinePerformanceOptions
            {
                DestinationBatching = new PipelineBatchingOptions
                {
                    BatchSize = 2,
                    MaxBatchDelay = TimeSpan.FromSeconds(1)
                }
            })
            .WithInMemorySource(new object())
            .AddSegment(new PassThroughPerformanceSegment(), new object())
            .WithDestination(destination)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await pipeline.PublishAsync(new TestPipelineRecord { Data = "1" }).ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "2" }).ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "3" }).ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Equal(new[] { 2, 1 }, destination.BatchSizes.ToArray());

        var snapshot = pipeline.GetPerformanceSnapshot();
        var destinationSnapshot = snapshot.Components.Single(component => component.ComponentName.StartsWith("Destination:", StringComparison.Ordinal));
        Assert.Equal(3, destinationSnapshot.ProcessedCount);
        Assert.Equal(0, destinationSnapshot.FaultedCount);
    }

    [Fact]
    public void Pipeline_Batching_Fails_For_NonBatched_Destinations()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Pipeline<TestPipelineRecord>.New("performance-invalid-batching")
                .UsePerformanceOptions(new PipelinePerformanceOptions
                {
                    DestinationBatching = new PipelineBatchingOptions { BatchSize = 2 }
                })
                .WithInMemorySource(new object())
                .AddSegment(new PassThroughPerformanceSegment(), new object())
                .WithInMemoryDestination("config")
                .Build());

        Assert.Contains("does not support batch execution", exception.Message);
    }

    private static void AssertExecutionOptions(
        PipelineExecutionOptions actual,
        int boundedCapacity,
        int degreeOfParallelism,
        bool ensureOrdered)
    {
        Assert.Equal(boundedCapacity, actual.BoundedCapacity);
        Assert.Equal(degreeOfParallelism, actual.DegreeOfParallelism);
        Assert.Equal(ensureOrdered, actual.EnsureOrdered);
    }
}
