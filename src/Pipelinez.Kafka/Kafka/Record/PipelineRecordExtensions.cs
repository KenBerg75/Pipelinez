using System.Text;
using Confluent.Kafka;
using Pipelinez.Core.Record;

namespace Pipelinez.Kafka.Record;

public static class PipelineRecordExtensions
{
    internal static PipelineRecordHeader ToPipelineRecordHeader(this IHeader header)
    {
        return new PipelineRecordHeader
        {
            Key = header.Key,
            Value = Encoding.UTF8.GetString(header.GetValueBytes())
        };
    }
    
    internal static Header ToKafkaHeader(this PipelineRecordHeader header)
    {
        return new Header(header.Key, Encoding.UTF8.GetBytes(header.Value));
    }
}