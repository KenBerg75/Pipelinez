using System.Collections.Concurrent;
using Confluent.Kafka;
using Example.Kafka;
using Example.Kafka.Segments;
using Examples.Shared;
using Microsoft.Extensions.Logging;
using Pipelinez.Core;
using Pipelinez.Kafka;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

var loggerConfiguration = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Async(sink => sink.Console(theme: AnsiConsoleTheme.Code));

Log.Logger = loggerConfiguration.CreateLogger();
var loggerFactory = new LoggerFactory().AddSerilog(Log.Logger);

var settings = KafkaExampleSettings.LoadFromEnvironment();
using var runtimeCancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    runtimeCancellation.Cancel();
};

await using var kafkaCluster = await KafkaExampleCluster.StartAsync(settings, runtimeCancellation.Token);
await kafkaCluster.EnsureTopicsAsync(runtimeCancellation.Token);

Log.Information("Kafka example starting");
Log.Information("BootstrapServers: {BootstrapServers}", kafkaCluster.BootstrapServers);
Log.Information("SourceTopic: {SourceTopic}", kafkaCluster.SourceTopic);
Log.Information("DestinationTopic: {DestinationTopic}", kafkaCluster.DestinationTopic);
Log.Information("ConsumerGroup: {ConsumerGroup}", kafkaCluster.ConsumerGroup);
Log.Information("Press Ctrl+C to stop early.");

var completedKeys = new ConcurrentQueue<string>();
var faultedKeys = new ConcurrentQueue<string>();
Exception? pipelineFault = null;

var pipeline = Pipeline<KafkaRecordJSON>.New("KafkaJsonPipeline")
    .UseLogger(loggerFactory)
    .WithKafkaSource(kafkaCluster.CreateSourceOptions(), (string key, string value) =>
        new KafkaRecordJSON
        {
            RecordKey = key,
            RecordValue = value
        })
    .AddSegment(new GoogleSegment(), "config")
    .WithKafkaDestination(kafkaCluster.CreateDestinationOptions(), (KafkaRecordJSON record) =>
        new Message<string, string>
        {
            Key = record.RecordKey,
            Value = record.RecordValue
        })
    .Build();

pipeline.OnPipelineRecordCompleted += (_, args) =>
{
    completedKeys.Enqueue(args.Record.RecordKey);
    Console.WriteLine($"Completed [{args.Record.RecordKey}] -> {args.Record.RecordValue[..Math.Min(args.Record.RecordValue.Length, 80)]}...");
};

pipeline.OnPipelineRecordFaulted += (_, args) =>
{
    faultedKeys.Enqueue(args.Record.RecordKey);
    Log.Error(
        args.Fault.Exception,
        "Record faulted. Key={RecordKey}, Component={ComponentName}",
        args.Record.RecordKey,
        args.Fault.ComponentName);
};

pipeline.OnPipelineFaulted += (_, args) =>
{
    pipelineFault = args.Exception;
    Log.Error(args.Exception, "Pipeline faulted in component {ComponentName}", args.ComponentName);
};

var pipelineStarted = false;

try
{
    await pipeline.StartPipelineAsync(runtimeCancellation.Token);
    pipelineStarted = true;

    await kafkaCluster.ProduceAsync(
        Enumerable.Range(0, kafkaCluster.MessageCount)
            .Select(index => new Message<string, string>
            {
                Key = $"example-key-{index}",
                Value = $"example-value-{index}",
                Headers = new Headers
                {
                    { "example-source", System.Text.Encoding.UTF8.GetBytes("Example.Kafka") }
                }
            }),
        runtimeCancellation.Token);

    await WaitForConditionAsync(
        () => completedKeys.Count + faultedKeys.Count >= kafkaCluster.MessageCount || pipelineFault is not null,
        TimeSpan.FromSeconds(30),
        runtimeCancellation.Token);

    await pipeline.CompleteAsync();
    await pipeline.Completion;

    Log.Information(
        "Kafka example completed. Successful={CompletedCount}, Faulted={FaultedCount}",
        completedKeys.Count,
        faultedKeys.Count);
    Log.Information("Run Example.Kafka.DataGen to publish more records to the same source topic if desired.");
}
catch (OperationCanceledException) when (runtimeCancellation.IsCancellationRequested)
{
    Log.Warning("Kafka example cancelled by user.");
}
catch (Exception exception)
{
    Log.Fatal(exception, "Kafka example failed.");
}
finally
{
    if (pipelineStarted)
    {
        try
        {
            await pipeline.CompleteAsync();
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while completing the Kafka example pipeline.");
        }
    }

    Log.Information("Shutting down Kafka example.");
    Log.CloseAndFlush();
}

static async Task WaitForConditionAsync(
    Func<bool> predicate,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    var deadline = DateTimeOffset.UtcNow + timeout;

    while (DateTimeOffset.UtcNow < deadline)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (predicate())
        {
            return;
        }

        await Task.Delay(100, cancellationToken);
    }

    throw new TimeoutException("Timed out waiting for the Kafka example pipeline to process the demo records.");
}
