using Pipelinez.Core.Record;

namespace Pipelinez.PostgreSql.Tests.EndToEnd.Models;

public sealed class TestPostgreSqlRecord : PipelineRecord
{
    public required string Id { get; init; }

    public required string Value { get; set; }
}
