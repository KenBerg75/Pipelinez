using Pipelinez.Core.Record;

namespace Pipelinez.RabbitMQ.Tests.Models;

public sealed class TestRabbitMqRecord : PipelineRecord
{
    public required string Id { get; init; }

    public required string Value { get; init; }
}
