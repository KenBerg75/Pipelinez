using System.Globalization;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Source;

namespace Pipelinez.RabbitMQ.Record;

internal static class RabbitMqMetadataExtensions
{
    public const string TransportName = "RabbitMQ";

    public static MetadataCollection ExtractMetadata(
        this RabbitMqDeliveryContext delivery,
        RabbitMqSourceOptions options,
        string leaseId)
    {
        var metadata = new MetadataCollection();

        metadata.Set(RabbitMqMetadataKeys.QueueName, options.Queue.Name);
        SetIfPresent(metadata, RabbitMqMetadataKeys.Exchange, delivery.Exchange);
        SetIfPresent(metadata, RabbitMqMetadataKeys.RoutingKey, delivery.RoutingKey);
        SetIfPresent(metadata, RabbitMqMetadataKeys.ConsumerTag, delivery.ConsumerTag);
        metadata.Set(RabbitMqMetadataKeys.DeliveryTag, delivery.DeliveryTag.ToString(CultureInfo.InvariantCulture));
        metadata.Set(RabbitMqMetadataKeys.Redelivered, delivery.Redelivered.ToString(CultureInfo.InvariantCulture));

        if (delivery.Properties is not null)
        {
            SetIfPresent(metadata, RabbitMqMetadataKeys.MessageId, delivery.Properties.MessageId);
            SetIfPresent(metadata, RabbitMqMetadataKeys.CorrelationId, delivery.Properties.CorrelationId);
            SetIfPresent(metadata, RabbitMqMetadataKeys.ContentType, delivery.Properties.ContentType);
            SetIfPresent(metadata, RabbitMqMetadataKeys.ContentEncoding, delivery.Properties.ContentEncoding);
            metadata.Set(RabbitMqMetadataKeys.DeliveryMode, delivery.Properties.DeliveryMode.ToString());
            metadata.Set(RabbitMqMetadataKeys.Priority, delivery.Properties.Priority.ToString(CultureInfo.InvariantCulture));
            metadata.Set(RabbitMqMetadataKeys.Timestamp, delivery.Properties.Timestamp.UnixTime.ToString(CultureInfo.InvariantCulture));
            SetIfPresent(metadata, RabbitMqMetadataKeys.Expiration, delivery.Properties.Expiration);
            SetIfPresent(metadata, RabbitMqMetadataKeys.Type, delivery.Properties.Type);
            SetIfPresent(metadata, RabbitMqMetadataKeys.UserId, delivery.Properties.UserId);
            SetIfPresent(metadata, RabbitMqMetadataKeys.AppId, delivery.Properties.AppId);
            SetIfPresent(metadata, RabbitMqMetadataKeys.ClusterId, delivery.Properties.ClusterId);
        }

        metadata.Set(DistributedMetadataKeys.TransportName, TransportName);
        metadata.Set(DistributedMetadataKeys.LeaseId, leaseId);
        metadata.Set(DistributedMetadataKeys.PartitionKey, options.Queue.Name);
        metadata.Set(DistributedMetadataKeys.Offset, delivery.DeliveryTag.ToString(CultureInfo.InvariantCulture));

        return metadata;
    }

    public static string BuildLeaseId(RabbitMqSourceOptions options)
    {
        var virtualHost = options.Connection.VirtualHost;
        if (string.IsNullOrWhiteSpace(virtualHost) && options.Connection.Uri is not null)
        {
            virtualHost = options.Connection.Uri.AbsolutePath.Trim('/');
        }

        if (string.IsNullOrWhiteSpace(virtualHost))
        {
            virtualHost = "/";
        }

        return $"rabbitmq:{virtualHost}:{options.Queue.Name}";
    }

    private static void SetIfPresent(MetadataCollection metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata.Set(key, value);
        }
    }
}
