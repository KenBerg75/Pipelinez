namespace Pipelinez.AzureServiceBus.Configuration;

/// <summary>
/// Configures how source messages are settled after Pipelinez terminal handling.
/// </summary>
public sealed class AzureServiceBusSourceSettlementOptions
{
    /// <summary>
    /// Gets or sets how source messages are settled when Pipelinez chooses a dead-letter action.
    /// </summary>
    public AzureServiceBusPipelineDeadLetterSettlement PipelineDeadLetterSettlement { get; set; }
        = AzureServiceBusPipelineDeadLetterSettlement.CompleteSourceMessage;

    /// <summary>
    /// Gets or sets a value indicating whether source messages should be abandoned when Pipelinez stops or rethrows.
    /// </summary>
    public bool AbandonOnStopOrRethrow { get; set; } = true;

    /// <summary>
    /// Validates the settlement options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public AzureServiceBusSourceSettlementOptions Validate()
    {
        if (!Enum.IsDefined(PipelineDeadLetterSettlement))
        {
            throw new InvalidOperationException(
                $"Unsupported Azure Service Bus dead-letter settlement '{PipelineDeadLetterSettlement}'.");
        }

        return this;
    }
}
