using Pipelinez.Core.Record;

namespace Pipelinez.SqlServer.Tests.EndToEnd.Models;

public sealed class TestSqlServerRecord : PipelineRecord
{
    public required string Id { get; init; }

    public required string Value { get; set; }
}
