namespace Pipelinez.AzureServiceBus.Source;

internal sealed class AzureServiceBusPendingMessage
{
    private readonly TaskCompletionSource<AzureServiceBusSettlementDecision> _settlementSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AzureServiceBusPendingMessage(string settlementKey, long sequenceNumber)
    {
        SettlementKey = settlementKey;
        SequenceNumber = sequenceNumber;
    }

    public string SettlementKey { get; }

    public long SequenceNumber { get; }

    public Task<AzureServiceBusSettlementDecision> Settlement => _settlementSource.Task;

    public bool TrySetSettlement(AzureServiceBusSettlementDecision decision)
    {
        return _settlementSource.TrySetResult(decision);
    }

    public bool TryCancel()
    {
        return _settlementSource.TrySetCanceled();
    }
}
