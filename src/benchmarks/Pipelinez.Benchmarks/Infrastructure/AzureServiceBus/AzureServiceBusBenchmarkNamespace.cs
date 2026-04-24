using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Pipelinez.AzureServiceBus.Configuration;

namespace Pipelinez.Benchmarks;

internal sealed class AzureServiceBusBenchmarkNamespace : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ServiceBusAdministrationClient _administrationClient;
    private readonly ServiceBusClient _client;

    public AzureServiceBusBenchmarkNamespace(string connectionString)
    {
        _connectionString = connectionString;
        _administrationClient = new ServiceBusAdministrationClient(connectionString);
        _client = new ServiceBusClient(connectionString);
    }

    public AzureServiceBusConnectionOptions CreateConnectionOptions()
    {
        return new AzureServiceBusConnectionOptions
        {
            ConnectionString = _connectionString
        };
    }

    public async Task CreateQueueAsync(string queueName)
    {
        await _administrationClient.CreateQueueAsync(queueName).ConfigureAwait(false);
    }

    public async Task DeleteQueueIfExistsAsync(string queueName)
    {
        if (await _administrationClient.QueueExistsAsync(queueName).ConfigureAwait(false))
        {
            await _administrationClient.DeleteQueueAsync(queueName).ConfigureAwait(false);
        }
    }

    public async Task SendMessagesAsync(string queueName, IEnumerable<ServiceBusMessage> messages)
    {
        await using var sender = _client.CreateSender(queueName);

        foreach (var message in messages)
        {
            await sender.SendMessageAsync(message).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
        string queueName,
        int expectedCount,
        TimeSpan timeout)
    {
        var receivedMessages = new List<ServiceBusReceivedMessage>(expectedCount);
        var deadline = DateTimeOffset.UtcNow + timeout;

        await using var receiver = _client.CreateReceiver(queueName);

        while (DateTimeOffset.UtcNow < deadline && receivedMessages.Count < expectedCount)
        {
            var batchSize = Math.Min(100, expectedCount - receivedMessages.Count);
            var messages = await receiver
                .ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1))
                .ConfigureAwait(false);

            if (messages.Count == 0)
            {
                continue;
            }

            foreach (var message in messages)
            {
                receivedMessages.Add(message);
                await receiver.CompleteMessageAsync(message).ConfigureAwait(false);
            }
        }

        return receivedMessages;
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
    }
}
