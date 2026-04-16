namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Identifies the RabbitMQ exchange type used when Pipelinez declares exchanges.
/// </summary>
public enum RabbitMqExchangeType
{
    /// <summary>
    /// Routes messages by exact routing-key match.
    /// </summary>
    Direct,

    /// <summary>
    /// Routes messages to all bound queues.
    /// </summary>
    Fanout,

    /// <summary>
    /// Routes messages by topic-pattern routing keys.
    /// </summary>
    Topic,

    /// <summary>
    /// Routes messages by header values.
    /// </summary>
    Headers
}
