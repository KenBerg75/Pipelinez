using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.AzureServiceBus.Tests.Models;
using Pipelinez.Core;
using Xunit;

namespace Pipelinez.AzureServiceBus.Tests.EndToEnd;

public class AzureServiceBusPipelineLiveTests
{
    [Fact]
    public async Task AzureServiceBus_Queue_Source_Consumes_And_Completes_Message_When_Configured()
    {
        var connectionString = Environment.GetEnvironmentVariable("PIPELINEZ_ASB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var queueName = $"pipelinez-test-{Guid.NewGuid():N}";
        var administrationClient = new ServiceBusAdministrationClient(connectionString);
        await administrationClient.CreateQueueAsync(queueName);

        await using var client = new ServiceBusClient(connectionString);
        var sender = client.CreateSender(queueName);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("hello")) { MessageId = "live-1" });

        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TestAzureServiceBusRecord? completedRecord = null;
        var connection = new AzureServiceBusConnectionOptions { ConnectionString = connectionString };

        var pipeline = Pipeline<TestAzureServiceBusRecord>.New("asb-live")
            .WithAzureServiceBusSource(
                new AzureServiceBusSourceOptions
                {
                    Connection = connection,
                    Entity = AzureServiceBusEntityOptions.ForQueue(queueName)
                },
                message => new TestAzureServiceBusRecord
                {
                    Id = message.MessageId,
                    Value = message.Body.ToString()
                })
            .WithInMemoryDestination("live")
            .Build();

        pipeline.OnPipelineRecordCompleted += (_, args) =>
        {
            completedRecord = args.Record;
            completed.TrySetResult();
        };

        try
        {
            await pipeline.StartPipelineAsync();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await pipeline.CompleteAsync();

            Assert.NotNull(completedRecord);
            Assert.Equal("live-1", completedRecord.Id);
            Assert.Equal("hello", completedRecord.Value);

            var receiver = client.CreateReceiver(queueName);
            var remaining = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
            Assert.Null(remaining);
        }
        finally
        {
            if (await administrationClient.QueueExistsAsync(queueName))
            {
                await administrationClient.DeleteQueueAsync(queueName);
            }
        }
    }
}
