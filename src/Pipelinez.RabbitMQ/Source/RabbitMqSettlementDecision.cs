namespace Pipelinez.RabbitMQ.Source;

internal readonly record struct RabbitMqSettlementDecision(
    RabbitMqSettlementAction Action,
    bool Requeue)
{
    public static RabbitMqSettlementDecision Ack { get; } = new(RabbitMqSettlementAction.Ack, Requeue: false);

    public static RabbitMqSettlementDecision None { get; } = new(RabbitMqSettlementAction.None, Requeue: false);

    public static RabbitMqSettlementDecision Nack(bool requeue)
    {
        return new RabbitMqSettlementDecision(RabbitMqSettlementAction.Nack, requeue);
    }

    public static RabbitMqSettlementDecision Reject(bool requeue)
    {
        return new RabbitMqSettlementDecision(RabbitMqSettlementAction.Reject, requeue);
    }
}
