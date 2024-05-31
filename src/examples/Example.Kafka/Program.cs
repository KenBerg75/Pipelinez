using Example.Kafka;
using Example.Kafka.Segments;
using Microsoft.Extensions.Logging;
using Pipelinez.Core;
using Pipelinez.Kafka.Configuration;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

var loggerConfiguration = new LoggerConfiguration()
    //.MinimumLevel.Verbose()
    .Enrich.FromLogContext();

    loggerConfiguration.WriteTo.Async(a =>
        a.Console(theme: AnsiConsoleTheme.Code));

Log.Logger = loggerConfiguration.CreateLogger();
var loggerFactory = new LoggerFactory()
    .AddSerilog(Log.Logger);

var token = new CancellationTokenSource();
KafkaSourceOptions sourceOptions = new KafkaSourceOptions()
{
    BootstrapServers = "",
    BootstrapUser = "",
    BootstrapPassword = "",
    ConsumerGroup = "",
    TopicName = ""
};

KafkaDestinationOptions destinationOptions = new KafkaDestinationOptions()
{
    BootstrapServers = "",
    BootstrapUser = "",
    BootstrapPassword = "",
    TopicName = ""
};
    
Log.Information("Starting up");

var pipeline = Pipeline<KafkaRecordJSON>.New("KafkaJsonPipeline")
    .UseLogger(loggerFactory) // logger should always come first. TODO: enforce this in the builder
    .WithKafkaSource<string, string>(sourceOptions, (key, value) =>
    {
        return new KafkaRecordJSON()
        {
            RecordKey = key,
            RecordValue = value
        };
    })
    .AddSegment(new GoogleSegment(), "config")
    .WithKafkaDestination<string, string>(destinationOptions, record =>
    {
        return new Confluent.Kafka.Message<string, string>()
        {
            Key = record.RecordKey,
            Value = record.RecordValue
        };
    })
    .Build();

var timer = new System.Timers.Timer(1000);
timer.Elapsed += (sender, args) =>
{
    Log.Information("status: {@Status}", pipeline.GetStatus());
};
//timer.Start();

// Output
pipeline.OnPipelineRecordCompleted += (sender, handlerArgs) =>
{
    Console.WriteLine($"[{handlerArgs.Record.RecordKey}]: {handlerArgs.Record.RecordValue}");
};


try
{
    pipeline.StartPipelineAsync(token);
    
    Thread.Sleep(10000);

    //await pipeline.CompleteAsync();
    
    //Thread.Sleep(10000);
    
    await pipeline.Completion;

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    timer.Dispose();
    await pipeline.CompleteAsync();
    Log.Information("Application shutting down");
    Log.CloseAndFlush();
}