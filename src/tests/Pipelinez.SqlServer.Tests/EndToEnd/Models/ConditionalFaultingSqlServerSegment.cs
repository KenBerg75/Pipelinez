using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;

namespace Pipelinez.SqlServer.Tests.EndToEnd.Models;

internal sealed class ConditionalFaultingSqlServerSegment(string badValue) : PipelineSegment<TestSqlServerRecord>
{
    public override Task<TestSqlServerRecord> ExecuteAsync(TestSqlServerRecord record)
    {
        if (record.Value == badValue)
        {
            throw new InvalidOperationException($"Value '{badValue}' is configured to fail.");
        }

        record.Value += "|processed";
        return Task.FromResult(record);
    }
}
