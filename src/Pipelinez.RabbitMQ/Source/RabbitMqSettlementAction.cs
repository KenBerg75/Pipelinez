namespace Pipelinez.RabbitMQ.Source;

internal enum RabbitMqSettlementAction
{
    Ack,
    Nack,
    Reject,
    None
}
