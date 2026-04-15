namespace Pipelinez.AzureServiceBus.Source;

internal sealed class AzureServiceBusSettlementDecision
{
    public AzureServiceBusSettlementDecision(
        AzureServiceBusSettlementAction action,
        string? deadLetterReason = null,
        string? deadLetterDescription = null)
    {
        Action = action;
        DeadLetterReason = deadLetterReason;
        DeadLetterDescription = deadLetterDescription;
    }

    public AzureServiceBusSettlementAction Action { get; }

    public string? DeadLetterReason { get; }

    public string? DeadLetterDescription { get; }

    public static AzureServiceBusSettlementDecision Complete { get; } = new(AzureServiceBusSettlementAction.Complete);

    public static AzureServiceBusSettlementDecision Abandon { get; } = new(AzureServiceBusSettlementAction.Abandon);

    public static AzureServiceBusSettlementDecision None { get; } = new(AzureServiceBusSettlementAction.None);

    public static AzureServiceBusSettlementDecision DeadLetter(string reason, string description)
    {
        return new AzureServiceBusSettlementDecision(AzureServiceBusSettlementAction.DeadLetter, reason, description);
    }
}
