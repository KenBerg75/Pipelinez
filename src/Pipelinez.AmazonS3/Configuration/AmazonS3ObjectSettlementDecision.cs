namespace Pipelinez.AmazonS3.Configuration;

internal readonly record struct AmazonS3ObjectSettlementDecision(
    AmazonS3ObjectSettlementAction Action,
    string? TargetPrefix,
    IReadOnlyDictionary<string, string> Tags)
{
    public static AmazonS3ObjectSettlementDecision Leave { get; } = new(
        AmazonS3ObjectSettlementAction.Leave,
        null,
        new Dictionary<string, string>());
}
