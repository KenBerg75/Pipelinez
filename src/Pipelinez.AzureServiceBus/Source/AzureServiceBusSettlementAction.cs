namespace Pipelinez.AzureServiceBus.Source;

internal enum AzureServiceBusSettlementAction
{
    Complete,
    Abandon,
    DeadLetter,
    None
}
