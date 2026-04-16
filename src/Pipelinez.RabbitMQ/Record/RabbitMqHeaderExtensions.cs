using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Record;
using Pipelinez.RabbitMQ.Destination;
using Pipelinez.RabbitMQ.Source;
using RabbitMQ.Client;

namespace Pipelinez.RabbitMQ.Record;

internal static class RabbitMqHeaderExtensions
{
    public static void CopyHeadersToRecord(
        this RabbitMqDeliveryContext delivery,
        PipelineRecord record,
        ILogger logger)
    {
        if (delivery.Properties?.Headers is null)
        {
            return;
        }

        foreach (var header in delivery.Properties.Headers)
        {
            if (TryFormatHeaderValue(header.Value, out var value))
            {
                record.Headers.Add(new PipelineRecordHeader
                {
                    Key = header.Key,
                    Value = value
                });
                continue;
            }

            logger.LogDebug(
                "Skipping unsupported RabbitMQ header {HeaderKey} of type {HeaderType}.",
                header.Key,
                header.Value?.GetType().FullName ?? "<null>");
        }
    }

    public static BasicProperties PrepareProperties(
        this PipelineRecord record,
        RabbitMqPublishMessage message,
        bool persistentMessagesByDefault,
        ILogger logger)
    {
        var properties = message.Properties ?? new BasicProperties();
        properties.Headers ??= new Dictionary<string, object?>();

        foreach (var header in record.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            if (properties.Headers.ContainsKey(header.Key))
            {
                logger.LogDebug(
                    "RabbitMQ message header {HeaderKey} already exists; preserving consumer-provided value.",
                    header.Key);
                continue;
            }

            properties.Headers[header.Key] = header.Value;
        }

        if (persistentMessagesByDefault && properties.DeliveryMode == default)
        {
            properties.Persistent = true;
        }

        return properties;
    }

    public static void AddHeaderIfAbsent(
        this BasicProperties properties,
        string key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        properties.Headers ??= new Dictionary<string, object?>();
        if (!properties.Headers.ContainsKey(key))
        {
            properties.Headers[key] = value;
        }
    }

    private static bool TryFormatHeaderValue(object? value, out string text)
    {
        switch (value)
        {
            case null:
                text = string.Empty;
                return true;
            case string stringValue:
                text = stringValue;
                return true;
            case byte[] byteArray:
                text = Encoding.UTF8.GetString(byteArray);
                return true;
            case ReadOnlyMemory<byte> memory:
                text = Encoding.UTF8.GetString(memory.Span);
                return true;
            case bool boolValue:
                text = boolValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case byte byteValue:
                text = byteValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case sbyte sbyteValue:
                text = sbyteValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case short shortValue:
                text = shortValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case ushort ushortValue:
                text = ushortValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case int intValue:
                text = intValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case uint uintValue:
                text = uintValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case long longValue:
                text = longValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case ulong ulongValue:
                text = ulongValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case float floatValue:
                text = floatValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case double doubleValue:
                text = doubleValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case decimal decimalValue:
                text = decimalValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case Guid guidValue:
                text = guidValue.ToString("D");
                return true;
            case DateTime dateTimeValue:
                text = dateTimeValue.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                return true;
            case DateTimeOffset dateTimeOffsetValue:
                text = dateTimeOffsetValue.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                return true;
            case TimeSpan timeSpanValue:
                text = timeSpanValue.ToString("c", CultureInfo.InvariantCulture);
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }
}
