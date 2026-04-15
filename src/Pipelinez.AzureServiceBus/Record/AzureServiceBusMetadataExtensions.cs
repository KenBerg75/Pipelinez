using System.Globalization;
using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.AzureServiceBus.Record;

internal static class AzureServiceBusMetadataExtensions
{
    public const string TransportName = "AzureServiceBus";

    public static MetadataCollection ExtractMetadata(
        this ServiceBusReceivedMessage message,
        AzureServiceBusEntityOptions entity,
        string leaseId)
    {
        var metadata = new MetadataCollection();

        metadata.Set(AzureServiceBusMetadataKeys.EntityKind, entity.EntityKind.ToString());

        if (!string.IsNullOrWhiteSpace(entity.QueueName))
        {
            metadata.Set(AzureServiceBusMetadataKeys.QueueName, entity.QueueName);
        }

        if (!string.IsNullOrWhiteSpace(entity.TopicName))
        {
            metadata.Set(AzureServiceBusMetadataKeys.TopicName, entity.TopicName);
        }

        if (!string.IsNullOrWhiteSpace(entity.SubscriptionName))
        {
            metadata.Set(AzureServiceBusMetadataKeys.SubscriptionName, entity.SubscriptionName);
        }

        SetIfPresent(metadata, AzureServiceBusMetadataKeys.MessageId, message.MessageId);
        metadata.Set(AzureServiceBusMetadataKeys.SequenceNumber, message.SequenceNumber.ToString(CultureInfo.InvariantCulture));
        metadata.Set(AzureServiceBusMetadataKeys.EnqueuedTimeUtc, message.EnqueuedTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        metadata.Set(AzureServiceBusMetadataKeys.DeliveryCount, message.DeliveryCount.ToString(CultureInfo.InvariantCulture));
        SetIfPresent(metadata, AzureServiceBusMetadataKeys.LockToken, message.LockToken);
        metadata.Set(AzureServiceBusMetadataKeys.LockedUntilUtc, message.LockedUntil.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        SetIfPresent(metadata, AzureServiceBusMetadataKeys.SessionId, message.SessionId);
        SetIfPresent(metadata, AzureServiceBusMetadataKeys.CorrelationId, message.CorrelationId);
        SetIfPresent(metadata, AzureServiceBusMetadataKeys.Subject, message.Subject);
        SetIfPresent(metadata, AzureServiceBusMetadataKeys.ContentType, message.ContentType);
        SetIfPresent(metadata, AzureServiceBusMetadataKeys.ReplyTo, message.ReplyTo);

        metadata.Set(DistributedMetadataKeys.TransportName, TransportName);
        metadata.Set(DistributedMetadataKeys.LeaseId, leaseId);
        metadata.Set(DistributedMetadataKeys.PartitionKey, entity.GetPartitionKey());
        metadata.Set(DistributedMetadataKeys.Offset, message.SequenceNumber.ToString(CultureInfo.InvariantCulture));

        return metadata;
    }

    public static string BuildLeaseId(AzureServiceBusEntityOptions entity)
    {
        var entityPath = entity.GetEntityPath().Replace('\\', '/');
        return $"asb:{entity.EntityKind}:{entityPath}";
    }

    private static void SetIfPresent(MetadataCollection metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata.Set(key, value);
        }
    }
}
