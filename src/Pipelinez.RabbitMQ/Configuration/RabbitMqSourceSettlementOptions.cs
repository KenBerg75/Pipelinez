namespace Pipelinez.RabbitMQ.Configuration;

/// <summary>
/// Configures how RabbitMQ source deliveries are settled after terminal Pipelinez handling.
/// </summary>
public sealed class RabbitMqSourceSettlementOptions
{
    /// <summary>
    /// Gets or sets how the original source delivery is settled when Pipelinez chooses a dead-letter action.
    /// </summary>
    public RabbitMqPipelineDeadLetterSettlement PipelineDeadLetterSettlement { get; set; }
        = RabbitMqPipelineDeadLetterSettlement.AckSourceMessage;

    /// <summary>
    /// Gets or sets a value indicating whether stop or rethrow actions should requeue the source delivery.
    /// </summary>
    public bool RequeueOnStopOrRethrow { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether records rejected before pipeline admission should be requeued.
    /// </summary>
    public bool RequeueOnPublishAdmissionFailure { get; set; } = true;

    /// <summary>
    /// Validates the settlement options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public RabbitMqSourceSettlementOptions Validate()
    {
        if (!Enum.IsDefined(PipelineDeadLetterSettlement))
        {
            throw new InvalidOperationException(
                $"Unsupported RabbitMQ dead-letter settlement '{PipelineDeadLetterSettlement}'.");
        }

        return this;
    }
}
