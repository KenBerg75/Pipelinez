using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;

namespace Pipelinez.PostgreSql.Tests.EndToEnd.Models;

internal sealed class ConditionalFaultingPostgreSqlSegment(string badValue) : PipelineSegment<TestPostgreSqlRecord>
{
    public override Task<TestPostgreSqlRecord> ExecuteAsync(TestPostgreSqlRecord record)
    {
        if (record.Value == badValue)
        {
            throw new InvalidOperationException($"Value '{badValue}' is configured to fail.");
        }

        record.Value += "|processed";
        return Task.FromResult(record);
    }
}
