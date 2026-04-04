using Pipelinez.Core.Distributed;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Source;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.DistributedTests.Models;

public sealed class ManualDistributedSource : PipelineSourceBase<TestPipelineRecord>, IDistributedPipelineSource<TestPipelineRecord>
{
    private readonly object _stateLock = new();
    private IReadOnlyList<PipelinePartitionLease> _ownedPartitions = Array.Empty<PipelinePartitionLease>();
    private readonly Dictionary<string, PipelinePartitionExecutionState> _partitionExecutionStates = new(StringComparer.OrdinalIgnoreCase);

    public bool SupportsDistributedExecution => true;

    public string TransportName => "TestTransport";

    public IReadOnlyList<PipelinePartitionLease> GetOwnedPartitions()
    {
        lock (_stateLock)
        {
            return _ownedPartitions.ToArray();
        }
    }

    public IReadOnlyList<PipelinePartitionExecutionState> GetPartitionExecutionStates()
    {
        lock (_stateLock)
        {
            return _partitionExecutionStates.Values.ToArray();
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
        var changedStates = new List<PipelinePartitionExecutionState>();

        lock (_stateLock)
        {
            _ownedPartitions = _ownedPartitions
                .Concat(leases)
                .GroupBy(lease => lease.LeaseId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();

            foreach (var lease in leases)
            {
                _partitionExecutionStates[lease.LeaseId] = new PipelinePartitionExecutionState(
                    lease.LeaseId,
                    lease.PartitionKey,
                    lease.PartitionId,
                    isAssigned: true,
                    isDraining: false,
                    inFlightCount: 0,
                    highestCompletedOffset: null);
                changedStates.Add(_partitionExecutionStates[lease.LeaseId]);
            }
        }

        ParentPipeline.ReportAssignedPartitions(leases);
        foreach (var changedState in changedStates)
        {
            ParentPipeline.ReportPartitionExecutionStateChanged(changedState);
        }
    }

    public void RevokePartitions(params PipelinePartitionLease[] leases)
    {
        var drainingStates = new List<PipelinePartitionExecutionState>();
        var drainingPartitions = new List<PipelinePartitionLease>();
        var drainedPartitions = new List<PipelinePartitionLease>();

        lock (_stateLock)
        {
            var revokedIds = leases
                .Select(lease => lease.LeaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _ownedPartitions = _ownedPartitions
                .Where(lease => !revokedIds.Contains(lease.LeaseId))
                .ToArray();

            foreach (var lease in leases)
            {
                if (_partitionExecutionStates.TryGetValue(lease.LeaseId, out var state))
                {
                    var drainingState = new PipelinePartitionExecutionState(
                        state.LeaseId,
                        state.PartitionKey,
                        state.PartitionId,
                        isAssigned: false,
                        isDraining: true,
                        inFlightCount: state.InFlightCount,
                        highestCompletedOffset: state.HighestCompletedOffset);
                    _partitionExecutionStates[lease.LeaseId] = drainingState;
                    drainingPartitions.Add(lease);
                    drainingStates.Add(drainingState);
                    _partitionExecutionStates.Remove(lease.LeaseId);
                    drainedPartitions.Add(lease);
                }
            }
        }

        foreach (var drainingPartition in drainingPartitions)
        {
            ParentPipeline.ReportPartitionDraining(drainingPartition);
        }

        foreach (var drainingState in drainingStates)
        {
            ParentPipeline.ReportPartitionExecutionStateChanged(drainingState);
        }

        ParentPipeline.ReportRevokedPartitions(leases);

        foreach (var drainedPartition in drainedPartitions)
        {
            ParentPipeline.ReportPartitionDrained(drainedPartition);
        }
    }

    public void SetPartitionExecutionState(PipelinePartitionExecutionState state)
    {
        lock (_stateLock)
        {
            _partitionExecutionStates[state.LeaseId] = state;
        }

        ParentPipeline.ReportPartitionExecutionStateChanged(state);
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
