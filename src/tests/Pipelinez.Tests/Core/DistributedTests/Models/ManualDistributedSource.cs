using Pipelinez.Core.Distributed;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Source;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.DistributedTests.Models;

public sealed class ManualDistributedSource : PipelineSourceBase<TestPipelineRecord>, IDistributedPipelineSource<TestPipelineRecord>
{
    private readonly object _leaseLock = new();
    private IReadOnlyList<PipelinePartitionLease> _ownedPartitions = Array.Empty<PipelinePartitionLease>();

    public bool SupportsDistributedExecution => true;

    public string TransportName => "TestTransport";

    public IReadOnlyList<PipelinePartitionLease> GetOwnedPartitions()
    {
        lock (_leaseLock)
        {
            return _ownedPartitions.ToArray();
        }
    }

    public Task PublishDistributedAsync(TestPipelineRecord record, PipelinePartitionLease lease, long offset)
    {
        var metadata = new MetadataCollection();
        metadata.Set(DistributedMetadataKeys.TransportName, TransportName);
        metadata.Set(DistributedMetadataKeys.LeaseId, lease.LeaseId);
        metadata.Set(DistributedMetadataKeys.PartitionKey, lease.PartitionKey);
        metadata.Set(DistributedMetadataKeys.PartitionId, Convert.ToString(lease.PartitionId) ?? string.Empty);
        metadata.Set(DistributedMetadataKeys.Offset, Convert.ToString(offset));
        return PublishAsync(record, metadata);
    }

    public void AssignPartitions(params PipelinePartitionLease[] leases)
    {
        lock (_leaseLock)
        {
            _ownedPartitions = _ownedPartitions
                .Concat(leases)
                .GroupBy(lease => lease.LeaseId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();
        }

        ParentPipeline.ReportAssignedPartitions(leases);
    }

    public void RevokePartitions(params PipelinePartitionLease[] leases)
    {
        lock (_leaseLock)
        {
            var revokedIds = leases
                .Select(lease => lease.LeaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _ownedPartitions = _ownedPartitions
                .Where(lease => !revokedIds.Contains(lease.LeaseId))
                .ToArray();
        }

        ParentPipeline.ReportRevokedPartitions(leases);
    }

    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(50, cancellationToken.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    protected override void Initialize()
    {
    }
}
