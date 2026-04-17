using Pipelinez.Core.Destination;
using Pipelinez.Core.Record;

namespace Pipelinez.SqlServer.Tests.EndToEnd.Models;

internal sealed class CollectingDestination : PipelineDestination<TestSqlServerRecord>
{
    private readonly List<TestSqlServerRecord> _records = new();

    public IReadOnlyList<TestSqlServerRecord> Records => _records;

    protected override Task ExecuteAsync(TestSqlServerRecord record, CancellationToken cancellationToken)
    {
        _records.Add(record);
        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
    }
}
