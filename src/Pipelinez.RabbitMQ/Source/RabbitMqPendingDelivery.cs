namespace Pipelinez.RabbitMQ.Source;

internal sealed class RabbitMqPendingDelivery
{
    private readonly TaskCompletionSource<RabbitMqSettlementDecision> _settlement =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RabbitMqPendingDelivery(string settlementKey, ulong deliveryTag)
    {
        SettlementKey = settlementKey;
        DeliveryTag = deliveryTag;
    }

    public string SettlementKey { get; }

    public ulong DeliveryTag { get; }

    public Task<RabbitMqSettlementDecision> Settlement => _settlement.Task;

    public bool TrySetSettlement(RabbitMqSettlementDecision decision)
    {
        return _settlement.TrySetResult(decision);
    }

    public bool TryCancel()
    {
        return _settlement.TrySetCanceled();
    }
}
