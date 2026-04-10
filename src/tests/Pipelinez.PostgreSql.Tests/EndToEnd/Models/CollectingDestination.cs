using Pipelinez.Core.Destination;
using Pipelinez.Core.Record;

namespace Pipelinez.PostgreSql.Tests.EndToEnd.Models;

internal sealed class CollectingDestination : PipelineDestination<TestPostgreSqlRecord>
{
    private readonly List<TestPostgreSqlRecord> _records = new();

    public IReadOnlyList<TestPostgreSqlRecord> Records => _records;

    protected override Task ExecuteAsync(TestPostgreSqlRecord record, CancellationToken cancellationToken)
    {
        _records.Add(record);
        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
    }
}
