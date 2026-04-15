namespace Pipelinez.AzureServiceBus.Configuration;

/// <summary>
/// Controls how a source message is settled when Pipelinez resolves the record to a dead-letter action.
/// </summary>
public enum AzureServiceBusPipelineDeadLetterSettlement
{
    /// <summary>
    /// Complete the source message after Pipelinez dead-letter handling succeeds.
    /// </summary>
    CompleteSourceMessage,

    /// <summary>
    /// Move the original source message to the native Azure Service Bus dead-letter queue.
    /// </summary>
    DeadLetterSourceMessage
}
