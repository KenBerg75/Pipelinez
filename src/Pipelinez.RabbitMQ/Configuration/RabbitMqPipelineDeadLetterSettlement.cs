namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Controls how a RabbitMQ source settles the original delivery when Pipelinez chooses a dead-letter action.
/// </summary>
public enum RabbitMqPipelineDeadLetterSettlement
{
    /// <summary>
    /// Acknowledges the source delivery after Pipelinez dead-letter handling completes.
    /// </summary>
    AckSourceMessage,

    /// <summary>
    /// Negatively acknowledges the source delivery without requeueing so RabbitMQ can route it to a dead-letter exchange when configured.
    /// </summary>
    DeadLetterOrDiscardSourceMessage
}
