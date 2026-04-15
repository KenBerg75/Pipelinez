using System.Globalization;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.AzureServiceBus.Record;

internal static class AzureServiceBusHeaderExtensions
{
    public static void CopyApplicationPropertiesToHeaders(
        this ServiceBusReceivedMessage message,
        PipelineRecord record,
        ILogger logger)
    {
        foreach (var property in message.ApplicationProperties)
        {
            if (TryFormatApplicationProperty(property.Value, out var value))
            {
                record.Headers.Add(new PipelineRecordHeader
                {
                    Key = property.Key,
                    Value = value
                });
                continue;
            }

            logger.LogDebug(
                "Skipping unsupported Azure Service Bus application property {PropertyKey} of type {PropertyType}.",
                property.Key,
                property.Value?.GetType().FullName ?? "<null>");
        }
    }

    public static void CopyHeadersToApplicationProperties(
        this PipelineRecord record,
        ServiceBusMessage message,
        ILogger logger)
    {
        foreach (var header in record.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            if (message.ApplicationProperties.ContainsKey(header.Key))
            {
                logger.LogDebug(
                    "Azure Service Bus message application property {PropertyKey} already exists; preserving consumer-provided value.",
                    header.Key);
                continue;
            }

            message.ApplicationProperties[header.Key] = header.Value;
        }
    }

    private static bool TryFormatApplicationProperty(object? value, out string text)
    {
        switch (value)
        {
            case null:
                text = string.Empty;
                return true;
            case string stringValue:
                text = stringValue;
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
