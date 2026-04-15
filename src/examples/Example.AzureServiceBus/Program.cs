using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Pipelinez.AzureServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;

var connectionString = Environment.GetEnvironmentVariable("PIPELINEZ_EXAMPLE_ASB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Set PIPELINEZ_EXAMPLE_ASB_CONNECTION_STRING to run the Azure Service Bus example.");
    Console.WriteLine("Use a real namespace or the Azure Service Bus emulator connection string.");
    return;
}

var suffix = Guid.NewGuid().ToString("N")[..8];
var inputQueue = $"pipelinez-orders-in-{suffix}";
var outputQueue = $"pipelinez-orders-out-{suffix}";
var deadLetterQueue = $"pipelinez-orders-dlq-{suffix}";

var administrationClient = new ServiceBusAdministrationClient(connectionString);
await EnsureQueueAsync(administrationClient, inputQueue);
await EnsureQueueAsync(administrationClient, outputQueue);
await EnsureQueueAsync(administrationClient, deadLetterQueue);

var connection = new AzureServiceBusConnectionOptions
{
    ConnectionString = connectionString
};

var totalTerminalRecords = 3;
var terminalRecords = 0;
var allRecordsHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

var pipeline = Pipeline<OrderRecord>.New("azure-service-bus-orders")
    .WithAzureServiceBusSource(
        new AzureServiceBusSourceOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue(inputQueue),
            MaxConcurrentCalls = 2
        },
        message => new OrderRecord
        {
            Id = message.MessageId,
            Payload = message.Body.ToString(),
            ShouldFail = message.ApplicationProperties.TryGetValue("should-fail", out var value) &&
                         string.Equals(Convert.ToString(value), "true", StringComparison.OrdinalIgnoreCase)
        })
    .AddSegment(new ValidateOrderSegment(), new object())
    .WithAzureServiceBusDestination(
        new AzureServiceBusDestinationOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue(outputQueue)
        },
        record => new ServiceBusMessage(BinaryData.FromString(record.Payload.ToUpperInvariant()))
        {
            MessageId = record.Id
        })
    .WithAzureServiceBusDeadLetterDestination(
        new AzureServiceBusDeadLetterOptions
        {
            Connection = connection,
            Entity = AzureServiceBusEntityOptions.ForQueue(deadLetterQueue)
        },
        deadLetter => new ServiceBusMessage(BinaryData.FromString(deadLetter.Record.Payload))
        {
            MessageId = deadLetter.Record.Id
        })
    .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
    .Build();

pipeline.OnPipelineRecordCompleted += (_, _) => MarkHandled();
pipeline.OnPipelineRecordDeadLettered += (_, _) => MarkHandled();

await using var seedClient = new ServiceBusClient(connectionString);
var inputSender = seedClient.CreateSender(inputQueue);

await inputSender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("first")) { MessageId = "order-1" });
await inputSender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("second")) { MessageId = "order-2" });

var failingMessage = new ServiceBusMessage(BinaryData.FromString("broken")) { MessageId = "order-3" };
failingMessage.ApplicationProperties["should-fail"] = "true";
await inputSender.SendMessageAsync(failingMessage);

await pipeline.StartPipelineAsync();
await allRecordsHandled.Task.WaitAsync(TimeSpan.FromSeconds(30));
await pipeline.CompleteAsync();

Console.WriteLine($"Processed messages written to {outputQueue}:");
await PrintMessagesAsync(seedClient, outputQueue);

Console.WriteLine($"Dead-letter messages written to {deadLetterQueue}:");
await PrintMessagesAsync(seedClient, deadLetterQueue);

async Task EnsureQueueAsync(ServiceBusAdministrationClient client, string queueName)
{
    if (!await client.QueueExistsAsync(queueName))
    {
        await client.CreateQueueAsync(queueName);
    }
}

async Task PrintMessagesAsync(ServiceBusClient client, string queueName)
{
    await using var receiver = client.CreateReceiver(queueName);
    var messages = await receiver.ReceiveMessagesAsync(maxMessages: 10, maxWaitTime: TimeSpan.FromSeconds(3));

    foreach (var message in messages)
    {
        Console.WriteLine($"- {message.MessageId}: {message.Body}");
        await receiver.CompleteMessageAsync(message);
    }
}

void MarkHandled()
{
    if (Interlocked.Increment(ref terminalRecords) >= totalTerminalRecords)
    {
        allRecordsHandled.TrySetResult();
    }
}

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }

    public required string Payload { get; init; }

    public bool ShouldFail { get; init; }
}

public sealed class ValidateOrderSegment : PipelineSegment<OrderRecord>
{
    public override Task<OrderRecord> ExecuteAsync(OrderRecord arg)
    {
        if (arg.ShouldFail)
        {
            throw new InvalidOperationException($"Order {arg.Id} failed validation.");
        }

        return Task.FromResult(arg);
    }
}
