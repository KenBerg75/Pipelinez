using System.Globalization;
using System.Text.Json;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.AmazonS3.Record;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Record;

namespace Pipelinez.AmazonS3.DeadLettering;

internal static class AmazonS3DeadLetterObjectMapper
{
    public static AmazonS3PutObject Map<T>(
        PipelineDeadLetterRecord<T> record,
        AmazonS3DeadLetterOptions options)
        where T : PipelineRecord
    {
        var payload = JsonSerializer.Serialize(
            new AmazonS3DeadLetterEnvelope<T>
            {
                Record = record.Record,
                Fault = new AmazonS3DeadLetterFault
                {
                    ComponentName = record.Fault.ComponentName,
                    ComponentKind = record.Fault.ComponentKind.ToString(),
                    OccurredAtUtc = record.Fault.OccurredAtUtc,
                    Message = record.Fault.Message,
                    ExceptionType = record.Fault.Exception.GetType().FullName,
                    ExceptionMessage = record.Fault.Exception.Message
                },
                Metadata = record.Metadata.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                SegmentHistory = record.SegmentHistory,
                RetryHistory = record.RetryHistory,
                CreatedAtUtc = record.CreatedAtUtc,
                DeadLetteredAtUtc = record.DeadLetteredAtUtc,
                Distribution = record.Distribution
            },
            options.SerializerOptions);

        var sourceKey = record.Metadata.GetValue(AmazonS3MetadataKeys.ObjectKey);
        var key = BuildKey(options.KeyPrefix, record.DeadLetteredAtUtc, sourceKey);

        return new AmazonS3PutObject
        {
            Key = key,
            TextContent = payload,
            ContentType = "application/json",
            Metadata = new Dictionary<string, string>
            {
                ["pipelinez-deadletter-component"] = record.Fault.ComponentName,
                ["pipelinez-deadletter-kind"] = record.Fault.ComponentKind.ToString(),
                ["pipelinez-deadletter-occurred-at"] = record.Fault.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ["pipelinez-deadlettered-at"] = record.DeadLetteredAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ["pipelinez-pipeline-source-transport"] = record.Distribution.TransportName ?? string.Empty,
                ["pipelinez-pipeline-lease-id"] = record.Distribution.LeaseId ?? string.Empty,
                [DistributedMetadataKeys.TransportName] = record.Distribution.TransportName ?? string.Empty,
                [DistributedMetadataKeys.LeaseId] = record.Distribution.LeaseId ?? string.Empty
            }
        };
    }

    private static string BuildKey(
        string prefix,
        DateTimeOffset deadLetteredAtUtc,
        string? sourceKey)
    {
        var safeSource = string.IsNullOrWhiteSpace(sourceKey)
            ? "record"
            : string.Join("_", sourceKey.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        var suffix = $"{deadLetteredAtUtc:yyyy/MM/dd/HHmmssfffffff}-{Guid.NewGuid():N}-{safeSource}.json";
        return AmazonS3ObjectKey.Build(prefix, suffix);
    }
}
